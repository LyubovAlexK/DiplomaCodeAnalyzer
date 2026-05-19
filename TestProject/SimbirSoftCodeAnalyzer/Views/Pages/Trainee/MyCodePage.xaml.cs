using AI.Extractors;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Border = System.Windows.Controls.Border;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Paragraph = System.Windows.Documents.Paragraph;
using Path = System.IO.Path;
using Run = System.Windows.Documents.Run;
using Style = System.Windows.Style;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class MyCodePage : UserControl
    {
        private string? _selectedPath;
        private List<GraphNodeVisual> _nodes = new();
        private List<GraphEdgeVisual> _edges = new();
        private GraphNodeVisual? _selectedNode;
        private bool _isDragging;
        private Point _dragStart;
        private double _dragOffsetX, _dragOffsetY;
        private double _zoom = 1.0;
        private const double NODE_SIZE = 14;
        private const double SELECTED_SIZE = 20;

        public MyCodePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadProjectsAsync();

        }

        private async Task<string> GetActiveGigaChatKeyAsync(AppDbContext db)
        {
            var token = await db.AccessTokens
                .FirstOrDefaultAsync(t => t.Description == "GigaChat" && t.IsActive == true);

            if (token == null)
            {
                MessageBox.Show("Не найден активный токен GigaChat. Добавьте токен в разделе «Системные ресурсы» (Администратор).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                throw new InvalidOperationException("Токен GigaChat не найден");
            }

            return token.TokenValue;
        }

        private async Task LoadProjectsAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;
                var projects = await db.Projects
                    .Where(p => p.TraineeId == myId && p.IsArchived != true)
                    .ToListAsync();
                ProjectCombo.Items.Clear();
                foreach (var p in projects)
                    ProjectCombo.Items.Add(new ComboBoxItem { Content = p.Title, Tag = p.ProjectId });
                if (ProjectCombo.Items.Count > 0) ProjectCombo.SelectedIndex = 0;
            }
            catch { }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Выберите папку с проектом" };
            if (dialog.ShowDialog() == true)
            {
                _selectedPath = dialog.FolderName;
                PathTextBox.Text = _selectedPath;
            }
        }
        private async Task SafeSave(AppDbContext db, string blockName)
        {
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                while (ex.InnerException != null) ex = ex.InnerException;
                MessageBox.Show($"Ошибка БД [{blockName}]: {msg}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath))
            {
                MessageBox.Show("Выберите папку с проектом");
                return;
            }
            HintText.Visibility = Visibility.Collapsed;
            ProgressBorder.Visibility = Visibility.Visible;
            try
            {
                await RunAnalysisAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nInner: {ex.InnerException?.Message}", "Ошибка анализа");
            }
            finally
            {
                ProgressBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RunAnalysisAsync()
        {
            var db = App.GetService<AppDbContext>();
            var roslynAnalyzer = App.GetService<RoslynSyntaxAnalyzer>();
            var roslynResultService = App.GetService<RoslynResultService>();
            var myId = App.CurrentUser?.UserId ?? 0;
            string apiKey = await GetActiveGigaChatKeyAsync(db);

            var selectedProject = ProjectCombo.SelectedItem as ComboBoxItem;
            int projectId = (int)(selectedProject?.Tag ?? 1);
            var project = await db.Projects.FindAsync(projectId);
            int specId = project?.SpecificationId ?? 4;

            // Сохраняем путь в проекте
            if (project != null && !string.IsNullOrEmpty(_selectedPath))
            {
                project.RepoUrl = _selectedPath;
                project.UpdatedAt = DateTime.Now;
                try
                {
                    await SafeSave(db, "блок сохранения пути");
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException;
                    while (inner != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"DB ERROR: {inner.Message}");
                        inner = inner.InnerException;
                    }
                    MessageBox.Show($"Ошибка сохранения: {ex.InnerException?.Message ?? ex.Message}");
                    return;
                }
            }

            // 1. Проверка на пустой проект
            StageText.Text = "Проверка проекта...";
            AnalysisProgress.Value = 0;

            var validator = App.GetService<ProjectValidator>();
            var emptyCheck = validator.CheckIfEmpty(_selectedPath!);
            if (emptyCheck.IsEmpty)
            {
                MessageBox.Show(emptyCheck.Reason, "Проект пустой", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Создание сессии
            StageText.Text = "Создание сессии...";
            AnalysisProgress.Value = 5;
            await Task.Delay(200);

            var session = new SessionAnalysis
            {
                TraineeId = myId,
                ProjectId = projectId,
                SpecificationId = specId,
                StartTime = DateTime.Now,
                Status = "InProgress",
                IsAiAvailable = true,
                IsArchived = false
            };
            db.SessionAnalysis.Add(session);
            await SafeSave(db, "блок создания сессии");

            // 3. Roslyn-анализ
            StageText.Text = "Анализ синтаксиса (Roslyn)...";
            AnalysisProgress.Value = 15;
            await Task.Delay(200);

            var result = await roslynAnalyzer.AnalyzeProjectAsync(_selectedPath!);
            await roslynResultService.SaveResultsAsync(session.SessionId, result);
            AnalysisProgress.Value = 35;

            // 4. NetArchTest
            StageText.Text = "Проверка архитектуры (NetArchTest)...";
            AnalysisProgress.Value = 40;
            await Task.Delay(200);

            try
            {
                string dllPath = Path.Combine(_selectedPath!, @"bin\Debug\net8.0", Path.GetFileName(_selectedPath!) + ".dll");
                if (File.Exists(dllPath))
                {
                    var archAnalyzer = App.GetService<ArchitectureAnalyzer>();
                    var rules = archAnalyzer.LoadRules(db, projectId);
                    if (rules.Any())
                    {
                        var archViolations = archAnalyzer.Analyze(dllPath, rules);
                        await roslynResultService.SaveNetArchResultsAsync(session.SessionId, archViolations);
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"NetArchTest: {ex.Message}"); }
            AnalysisProgress.Value = 55;

            // 5. AI Judge
            StageText.Text = "Семантический анализ (AI Judge)...";
            AnalysisProgress.Value = 60;
            await Task.Delay(200);

            try
            {
                var dictService = App.GetService<Core.Services.DictionaryService>();
                var gigaExtractor = new AI.Extractors.GigaChatExtractor(apiKey, dictService);
                var token = await gigaExtractor.GetAccessTokenAsync();
                var aiJudge = new AISemanticJudge(db, token);

                var allCode = string.Join("\n", Directory.GetFiles(_selectedPath!, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                    .Select(f => File.ReadAllText(f)));

                if (allCode.Length > 0)
                    await aiJudge.JudgeAsync(session.SessionId, specId, allCode);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AI Judge: {ex.Message}"); }
            AnalysisProgress.Value = 80;

            // 6. Сравнение с эталоном
            StageText.Text = "Сравнение с эталоном...";
            AnalysisProgress.Value = 85;
            await Task.Delay(200);

            try
            {
                var referenceService = App.GetService<ReferenceService>();
                var reference = await db.ReferenceProjects
                    .FirstOrDefaultAsync(r => r.SpecificationId == specId && r.IsActive == true);

                if (reference != null)
                {
                    var refResult = await referenceService.CompareWithReference(reference.ReferenceId, result);
                    foreach (var v in refResult)
                    {
                        db.AuditVerdicts.Add(new AuditVerdict
                        {
                            SessionId = session.SessionId,
                            RequirementId = null,
                            IsPassed = false,
                            Reason = v,
                            AiModel = "Reference",
                            Confidence = 0.9m
                        });
                    }

                    int totalChecks = 4;
                    int failedChecks = Math.Min(refResult.Count, totalChecks);
                    session.ReferenceMatchPercent = Math.Max(0, (decimal)(totalChecks - failedChecks) / totalChecks * 100);
                }

                // Roslyn-процент (синтаксис)
                session.OverallMatchPercent = result.TotalMethods > 0
                    ? 100 - (decimal)result.MethodsExceedingComplexity / result.TotalMethods * 100 : 100;

                try
                {
                    await SafeSave(db, "блок 6");
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException;
                    while (inner != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"DB ERROR (блок 6): {inner.Message}");
                        inner = inner.InnerException;
                    }
                    MessageBox.Show($"Ошибка сохранения: {ex.InnerException?.Message ?? ex.Message}");
                    return;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Эталон: {ex.Message}"); }
            AnalysisProgress.Value = 95;

            // 7. Завершение
            session.EndTime = DateTime.Now;
            session.Status = "Completed";
            await SafeSave(db, "блок 7");

            AnalysisProgress.Value = 100;
            StageText.Text = "Анализ завершён!";
            await Task.Delay(500);

            // 8. Граф
            await BuildGraphAsync(_selectedPath!);

            // Показать панели
            GraphBorder.Visibility = Visibility.Visible;
            InfoPanel.Visibility = Visibility.Visible;
            HintText.Visibility = Visibility.Collapsed;

            MessageBox.Show($"Проверка завершена!\nСоответствие синтаксису: {session.OverallMatchPercent:F0}%\n" +
                $"Нарушений Roslyn: {result.MethodsExceedingComplexity}\n" +
                $"Соответствие эталону: {session.ReferenceMatchPercent:F0}%",
                "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task BuildGraphAsync(string projectPath)
        {
            GraphCanvas.Children.Clear();
            _nodes.Clear();
            _edges.Clear();

            var graphService = new GraphBuilderService();
            var graph = await graphService.BuildGraphAsync(projectPath);

            foreach (var node in graph.Nodes)
            {
                var visual = new GraphNodeVisual
                {
                    Id = node.Id,
                    Name = node.Name,
                    FullName = node.FullName,
                    Kind = node.Kind,
                    X = 350 + Random.Shared.Next(-200, 200),
                    Y = 250 + Random.Shared.Next(-200, 200),
                    HasViolation = node.HasViolations,
                    FilePath = node.FilePath,
                    LineNumber = node.LineNumber,
                    MethodCount = node.MethodCount,
                    Complexity = node.CyclomaticComplexity,
                    Category = node.Category
                };
                _nodes.Add(visual);
            }

            LayoutNodes(graph);

            foreach (var n in _nodes) DrawNode(n);

            foreach (var edge in graph.Edges)
            {
                var s = _nodes.FirstOrDefault(n => n.Id == edge.SourceId);
                var t = _nodes.FirstOrDefault(n => n.Id == edge.TargetId);
                if (s != null && t != null)
                {
                    var ve = new GraphEdgeVisual { Source = s, Target = t, RelationType = edge.RelationType, IsViolation = edge.IsViolation };
                    _edges.Add(ve);
                    DrawEdge(ve);
                }
            }

            FitCanvasToNodes();
            ScrollToCenter();

            // Обогащаем граф метриками из Roslyn
            var roslynAnalyzer = App.GetService<RoslynSyntaxAnalyzer>();
            var metricsResult = await roslynAnalyzer.AnalyzeProjectAsync(projectPath);

            foreach (var method in metricsResult.Methods)
            {
                var node = _nodes.FirstOrDefault(n =>
                    n.Name == method.ClassName ||
                    n.FullName.EndsWith("." + method.ClassName) ||
                    method.ClassName.EndsWith("." + n.Name));

                if (node != null)
                {
                    node.Complexity = Math.Max(node.Complexity, method.CyclomaticComplexity);
                    if (method.CyclomaticComplexity > 10)
                        node.HasViolation = true;
                }
            }

            foreach (var node in _nodes.Where(n => n.HasViolation))
            {
                RedrawNode(node);
            }
        }

        private void FitCanvasToNodes()
        {
            if (_nodes.Count == 0) return;

            double padding = 100;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var node in _nodes)
            {
                if (node.X < minX) minX = node.X;
                if (node.Y < minY) minY = node.Y;
                if (node.X > maxX) maxX = node.X;
                if (node.Y > maxY) maxY = node.Y;
            }

            minX -= padding; minY -= padding; maxX += padding; maxY += padding;

            double width = Math.Max(800, maxX - minX);
            double height = Math.Max(600, maxY - minY);

            GraphCanvas.Width = width;
            GraphCanvas.Height = height;

            double offsetX = minX < 0 ? -minX : 0;
            double offsetY = minY < 0 ? -minY : 0;

            if (offsetX > 0 || offsetY > 0)
            {
                foreach (var node in _nodes)
                {
                    node.X += offsetX;
                    node.Y += offsetY;
                    double size = node.IsSelected ? 20 : 14;
                    if (node.DotVisual != null)
                    {
                        Canvas.SetLeft(node.DotVisual, node.X - size / 2);
                        Canvas.SetTop(node.DotVisual, node.Y - size / 2);
                    }
                    if (node.LabelVisual != null)
                    {
                        Canvas.SetLeft(node.LabelVisual, node.X + size / 2 + 4);
                        Canvas.SetTop(node.LabelVisual, node.Y - 8);
                    }
                }
                foreach (var edge in _edges)
                    if (edge.Visual != null) UpdateLinePosition(edge.Visual, edge);
            }
        }

        private void ScrollToCenter()
        {
            var scrollViewer = GraphCanvas.Parent as ScrollViewer;
            if (scrollViewer == null || _nodes.Count == 0) return;
            GraphCanvas.UpdateLayout();
            scrollViewer.UpdateLayout();
            double centerX = (GraphCanvas.Width - scrollViewer.ViewportWidth) / 2;
            double centerY = (GraphCanvas.Height - scrollViewer.ViewportHeight) / 2;
            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, centerX));
            scrollViewer.ScrollToVerticalOffset(Math.Max(0, centerY));
        }

        private void LayoutNodes(ProjectGraph graph)
        {
            var nodeDict = _nodes.ToDictionary(n => n.Id);
            for (int iter = 0; iter < 5; iter++)
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    for (int j = i + 1; j < _nodes.Count; j++)
                    {
                        double dx = _nodes[i].X - _nodes[j].X;
                        double dy = _nodes[i].Y - _nodes[j].Y;
                        double dist = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                        double force = 5000 / (dist * dist);
                        _nodes[i].X += force * dx / dist + (Random.Shared.NextDouble() - 0.5) * 2;
                        _nodes[i].Y += force * dy / dist + (Random.Shared.NextDouble() - 0.5) * 2;
                        _nodes[j].X -= force * dx / dist + (Random.Shared.NextDouble() - 0.5) * 2;
                        _nodes[j].Y -= force * dy / dist + (Random.Shared.NextDouble() - 0.5) * 2;
                    }
                }
                foreach (var edge in graph.Edges)
                {
                    if (nodeDict.TryGetValue(edge.SourceId, out var s) && nodeDict.TryGetValue(edge.TargetId, out var t))
                    {
                        double dx = t.X - s.X, dy = t.Y - s.Y;
                        double dist = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                        double force = dist / 100;
                        s.X += force * dx / dist; s.Y += force * dy / dist;
                        t.X -= force * dx / dist; t.Y -= force * dy / dist;
                    }
                }
            }
        }

        private void DrawNode(GraphNodeVisual node)
        {
            double size = node.IsSelected ? SELECTED_SIZE : NODE_SIZE;

            Color normalColor = node.Category switch
            {
                "UI" => ((SolidColorBrush)FindResource("AccentDarkBrush")).Color,
                "Business" => ((SolidColorBrush)FindResource("PrimaryBrush")).Color,
                "DataAccess" => ((SolidColorBrush)FindResource("SuccessBrush")).Color,
                "Data" => ((SolidColorBrush)FindResource("AccentBrush")).Color,
                "Interface" => ((SolidColorBrush)FindResource("PrimaryLightBrush")).Color,
                _ => ((SolidColorBrush)FindResource("SecondaryTextBrush")).Color
            };

            Color color = node.IsSelected ? ((SolidColorBrush)FindResource("AccentDarkBrush")).Color :
                          node.HasViolation ? ((SolidColorBrush)FindResource("DangerBrush")).Color : normalColor;

            Color glowColor = node.IsSelected ? Colors.Gold :
                              node.HasViolation ? ((SolidColorBrush)FindResource("DangerBrush")).Color : normalColor;

            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = node.IsSelected ? 3 : 1.5,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = node.IsSelected ? 12 : 6,
                    ShadowDepth = 0,
                    Opacity = node.IsSelected ? 0.8 : 0.4,
                    Color = glowColor
                },
                Tag = node,
                Cursor = Cursors.Hand
            };
            dot.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            dot.MouseRightButtonDown += Node_MouseRightButtonDown;

            var label = new TextBlock
            {
                Text = node.Name.Length > 12 ? node.Name[..12] + "…" : node.Name,
                FontSize = node.IsSelected ? 11 : 9,
                FontFamily = new FontFamily("Inter"),
                FontWeight = node.IsSelected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
            };

            Canvas.SetLeft(dot, node.X - size / 2);
            Canvas.SetTop(dot, node.Y - size / 2);
            Canvas.SetLeft(label, node.X + size / 2 + 4);
            Canvas.SetTop(label, node.Y - 8);

            GraphCanvas.Children.Add(dot);
            GraphCanvas.Children.Add(label);

            node.DotVisual = dot;
            node.LabelVisual = label;
        }

        private void RedrawNode(GraphNodeVisual node)
        {
            if (node.DotVisual != null) GraphCanvas.Children.Remove(node.DotVisual);
            if (node.LabelVisual != null) GraphCanvas.Children.Remove(node.LabelVisual);
            DrawNode(node);
        }

        private void DrawEdge(GraphEdgeVisual edge)
        {
            var line = new Line
            {
                Stroke = edge.IsViolation
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                StrokeThickness = edge.IsViolation ? 2 : 1,
                StrokeDashArray = edge.IsViolation ? new DoubleCollection(new[] { 4.0, 3.0 }) : null
            };
            UpdateLinePosition(line, edge);
            Canvas.SetZIndex(line, -1);
            GraphCanvas.Children.Add(line);
            edge.Visual = line;
        }

        private void UpdateLinePosition(Line line, GraphEdgeVisual edge)
        {
            line.X1 = edge.Source.X; line.Y1 = edge.Source.Y;
            line.X2 = edge.Target.X; line.Y2 = edge.Target.Y;
        }

        private void UpdateAllEdges()
        {
            foreach (var edge in _edges)
                if (edge.Visual != null) UpdateLinePosition(edge.Visual, edge);
        }

        private void SelectNode(GraphNodeVisual? node)
        {
            if (_selectedNode != null) { _selectedNode.IsSelected = false; RedrawNode(_selectedNode); }
            _selectedNode = node;
            if (_selectedNode != null) { _selectedNode.IsSelected = true; RedrawNode(_selectedNode); }

            foreach (var n in _nodes)
            {
                if (n.DotVisual != null) n.DotVisual.Opacity = (_selectedNode == null || n == _selectedNode) ? 1.0 : 0.3;
                if (n.LabelVisual != null) n.LabelVisual.Opacity = (_selectedNode == null || n == _selectedNode) ? 1.0 : 0.3;
            }
            foreach (var e in _edges)
            {
                if (e.Visual != null)
                    e.Visual.Opacity = (_selectedNode == null) ? 1.0 : ((e.Source == _selectedNode || e.Target == _selectedNode) ? 1.0 : 0.15);
            }
            UpdateInfoPanel(node);
        }

        private void UpdateInfoPanel(GraphNodeVisual? node)
        {
            InfoContent.Children.Clear();

            if (node == null)
            {
                PlaceholderText.Visibility = Visibility.Visible;
                return;
            }

            PlaceholderText.Visibility = Visibility.Collapsed;

            // Заголовок
            InfoContent.Children.Add(new TextBlock
            {
                Text = node.Name,
                FontSize = (double)FindResource("AppFontSizeH2"),
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Разделитель
            InfoContent.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 10, 0, 12)
            });

            // Кнопка
            var openButton = new Button
            {
                Content = "Открыть код",
                Style = (Style)FindResource("OutlineButton"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = node,
                FontSize = (double)FindResource("AppFontSizeH4")
            };
            openButton.Click += OpenCodeWindow_Click;
            InfoContent.Children.Add(openButton);
        }
        private string GetCategoryName(string category) => category switch
        {
            "UI" => "Интерфейс",
            "Business" => "Бизнес-логика",
            "DataAccess" => "Доступ к данным",
            "Data" => "Модель данных",
            "Interface" => "Абстракции",
            _ => "Прочее"
        };

        private void AddInfoLine(string text)
        {
            InfoContent.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush"),
                Margin = new Thickness(0, 0, 0, 5)
            });
        }

        private async void OpenCodeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not GraphNodeVisual node ||
                string.IsNullOrEmpty(node.FilePath) || !File.Exists(node.FilePath)) return;

            var codeWindow = new Window
            {
                Title = $"Код класса: {node.Name}",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = Window.GetWindow(this),
                Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/logo-simbirsoft.png")),
                Background = Brushes.White
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = node.Name,
                FontSize = (double)FindResource("AppFontSizeH1"),
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryBrush"),
                Margin = new Thickness(24, 20, 24, 0)
            };
            Grid.SetRow(titleBlock, 0); grid.Children.Add(titleBlock);

            // Popup для подсказок (в этом окне)
            var helpPopup = new Popup
            {
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };
            var popupBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                MaxWidth = 350
            };
            popupBorder.Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = 0.15, Color = Colors.Black };
            var popupStack = new StackPanel();
            var helpTitle = new TextBlock
            {
                FontSize = (double)FindResource("AppFontSizeH3"),
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("DarkTextBrush"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var helpDesc = new TextBlock
            {
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };
            popupStack.Children.Add(helpTitle);
            popupStack.Children.Add(helpDesc);
            popupBorder.Child = popupStack;
            helpPopup.Child = popupBorder;
            grid.Children.Add(helpPopup);

            var infoStack = new StackPanel { Margin = new Thickness(24, 8, 24, 0) };
            infoStack.Children.Add(CreateInfoText($"Тип: {node.Kind}"));
            infoStack.Children.Add(CreateInfoText($"Категория: {GetCategoryName(node.Category)}"));

            // Методов
            var methodsRow = CreateHelpRowLocal("Методов: ", node.MethodCount.ToString(),
                "Количество методов в классе",
                "• 1–10 — норма\n• 11–25 — много\n• > 25 — критично\n\nРекомендация: при > 20 методов рассмотрите разделение класса.",
                helpPopup, helpTitle, helpDesc);
            infoStack.Children.Add(methodsRow);

            // Сложность
            var complexityRow = CreateHelpRowLocal("Сложность: ", $"{node.Complexity:F1} (цикломатическая)",
                "Цикломатическая сложность",
                "Мера количества независимых путей через код.\n\nПороги:\n• 1–10 — норма\n• 11–20 — повышенная\n• > 20 — высокая\n\nРекомендация: разбейте сложные методы на несколько простых.",
                helpPopup, helpTitle, helpDesc);
            infoStack.Children.Add(complexityRow);

            Grid.SetRow(infoStack, 1); grid.Children.Add(infoStack);

            var line = new Border
            {
                Height = 1,
                Background = (Brush)FindResource("BorderBrush"),
                Margin = new Thickness(24, 12, 24, 8)
            };
            Grid.SetRow(line, 2); grid.Children.Add(line);

            // Код
            var code = File.ReadAllText(node.FilePath);
            var codeBox = new RichTextBox
            {
                FontFamily = new FontFamily("Courier New"),
                FontSize = (double)FindResource("AppFontSizeH4"),
                IsReadOnly = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                Foreground = (Brush)FindResource("DarkTextBrush"),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(24, 0, 24, 20)
            };

            var doc = new FlowDocument();
            var para = new Paragraph();

            var verdicts = new List<AuditVerdict>();
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;
                var lastSession = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId)
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefaultAsync();
                if (lastSession != null)
                {
                    verdicts = await db.AuditVerdicts
                        .Where(v => v.SessionId == lastSession.SessionId && !v.IsPassed)
                        .ToListAsync();
                }
            }
            catch { }

            var lines = code.Split('\n');
            foreach (var codeLine in lines)
            {
                bool isViolationLine = false;
                foreach (var v in verdicts)
                {
                    if (!string.IsNullOrEmpty(v.CodeLocation) &&
                        !string.IsNullOrEmpty(codeLine) &&
                        v.CodeLocation.Contains(':') &&
                        codeLine.Contains(v.CodeLocation.Split(':').LastOrDefault() ?? ""))
                    {
                        isViolationLine = true;
                        break;
                    }
                }

                if (codeLine.Contains("class " + node.Name))
                {
                    int idx = codeLine.IndexOf("class " + node.Name);
                    para.Inlines.Add(new Run(codeLine[..idx]) { FontFamily = new FontFamily("Courier New") });
                    para.Inlines.Add(new Run(codeLine[idx..(idx + 6 + node.Name.Length)])
                    {
                        FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Courier New"),
                        Foreground = isViolationLine ? (Brush)FindResource("DangerBrush") : (Brush)FindResource("DarkTextBrush")
                    });
                    para.Inlines.Add(new Run(codeLine[(idx + 6 + node.Name.Length)..] + "\n")
                    {
                        FontFamily = new FontFamily("Courier New"),
                        Foreground = isViolationLine ? (Brush)FindResource("DangerBrush") : Brushes.Black
                    });
                }
                else if (isViolationLine)
                {
                    para.Inlines.Add(new Run(codeLine + "\n")
                    {
                        FontFamily = new FontFamily("Courier New"),
                        Foreground = (Brush)FindResource("DangerBrush"),
                        FontWeight = FontWeights.Bold
                    });
                }
                else
                {
                    para.Inlines.Add(new Run(codeLine + "\n") { FontFamily = new FontFamily("Courier New") });
                }
            }
            doc.Blocks.Add(para);
            codeBox.Document = doc;

            Grid.SetRow(codeBox, 3); grid.Children.Add(codeBox);
            codeWindow.Content = grid;
            codeWindow.Show();
        }

        private StackPanel CreateHelpRowLocal(string label, string value, string title, string description,
            Popup popup, TextBlock titleBlock, TextBlock descBlock)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            var textBlock = new TextBlock
            {
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.Inlines.Add(new Run(label));
            textBlock.Inlines.Add(new Run(value) { FontWeight = FontWeights.SemiBold });
            row.Children.Add(textBlock);

            var helpBtn = new Button
            {
                Content = "?",
                Width = 20,
                Height = 20,
                FontSize = 12,
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Background = (Brush)FindResource("CardBackgroundBrush"),
                Foreground = (Brush)FindResource("PrimaryBrush"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            helpBtn.Click += (s, ev) =>
            {
                titleBlock.Text = title;
                descBlock.Text = description;
                popup.PlacementTarget = helpBtn;
                popup.IsOpen = true;
            };
            row.Children.Add(helpBtn);

            return row;
        }
        private void ComplexityHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Цикломатическая сложность — мера количества независимых путей через исходный код.\n\n" +
                "Пороги:\n" +
                "• 1–10 — норма (зелёный)\n" +
                "• 11–20 — повышенная (жёлтый)\n" +
                "• > 20 — высокая (красный)\n\n" +
                "Рекомендация: разбейте сложные методы на несколько более простых.",
                "Цикломатическая сложность",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private StackPanel CreateHelpRow(string label, string value, string title, string description)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            // Текст строки
            var textBlock = new TextBlock
            {
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.Inlines.Add(new Run(label));
            textBlock.Inlines.Add(new Run(value) { FontWeight = FontWeights.SemiBold });
            row.Children.Add(textBlock);

            // Кнопка "?"
            var helpBtn = new Button
            {
                Content = "?",
                Width = 20,
                Height = 20,
                FontSize = 12,
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Background = (Brush)FindResource("CardBackgroundBrush"),
                Foreground = (Brush)FindResource("PrimaryBrush"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tag = new HelpInfo { Title = title, Description = description }
            };
            helpBtn.Click += HelpButtonInfo_Click;
            row.Children.Add(helpBtn);

            return row;
        }

        private class HelpInfo
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
        }

        private void HelpButtonInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is HelpInfo info)
            {
                HelpTitleText.Text = info.Title;
                HelpDescText.Text = info.Description;
                CodeHelpPopup.IsOpen = true;
                CodeHelpPopup.PlacementTarget = btn;
            }
        }

        private void HelpButton_Popup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                dynamic tag = btn.Tag;
                HelpTitleText.Text = tag.Title;
                HelpDescText.Text = tag.Text;
                CodeHelpPopup.PlacementTarget = btn;
                CodeHelpPopup.IsOpen = true;
            }
        }
        private void MethodsHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Количество методов в классе.\n\n" +
                "Рекомендация: если методов больше 20, рассмотрите разделение класса на несколько.",
                "Количество методов",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViolationsHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Наличие нарушений в классе, найденных анализаторами:\n" +
                "• Cинтаксические метрики (сложность, строки, вложенность)\n" +
                "• Архитектурные зависимости\n" +
                "• Cемантическое несоответствие ТЗ\n\n" +
                "Красный узел на графе = есть нарушения.",
                "Нарушения",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private TextBlock CreateInfoText(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush"),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse dot && dot.Tag is GraphNodeVisual node)
            {
                SelectNode(node);
                _isDragging = true;
                var pos = e.GetPosition(GraphCanvas);
                _dragStart = pos;
                _dragOffsetX = node.X - pos.X;
                _dragOffsetY = node.Y - pos.Y;
                e.Handled = true;
            }
        }

        private void Node_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectNode(null);
        }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var mousePos = e.GetPosition(GraphCanvas);
                double oldZoom = _zoom;
                _zoom += e.Delta > 0 ? 0.1 : -0.1;
                _zoom = Math.Max(0.3, Math.Min(3.0, _zoom));
                var scrollViewer = GraphCanvas.Parent as ScrollViewer;
                if (scrollViewer != null)
                {
                    double scaleFactor = _zoom / oldZoom;
                    double newOffsetX = mousePos.X * scaleFactor - mousePos.X + scrollViewer.HorizontalOffset;
                    double newOffsetY = mousePos.Y * scaleFactor - mousePos.Y + scrollViewer.VerticalOffset;
                    GraphCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
                    GraphCanvas.UpdateLayout();
                    scrollViewer.ScrollToHorizontalOffset(Math.Max(0, Math.Min(newOffsetX, scrollViewer.ScrollableWidth)));
                    scrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(newOffsetY, scrollViewer.ScrollableHeight)));
                }
                else GraphCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(GraphCanvas);
            var hitElement = GraphCanvas.InputHitTest(pos) as DependencyObject;
            bool clickedOnNode = false;
            while (hitElement != null)
            {
                if (hitElement is Ellipse ell && ell.Tag is GraphNodeVisual) { clickedOnNode = true; break; }
                hitElement = VisualTreeHelper.GetParent(hitElement);
            }
            if (!clickedOnNode) SelectNode(null);
        }

        private void GraphCanvas_MouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (_isDragging && _selectedNode != null)
            {
                var pos = e.GetPosition(GraphCanvas);
                _selectedNode.X = pos.X + _dragOffsetX;
                _selectedNode.Y = pos.Y + _dragOffsetY;
                Canvas.SetLeft(_selectedNode.DotVisual!, _selectedNode.X - SELECTED_SIZE / 2);
                Canvas.SetTop(_selectedNode.DotVisual!, _selectedNode.Y - SELECTED_SIZE / 2);
                Canvas.SetLeft(_selectedNode.LabelVisual!, _selectedNode.X + SELECTED_SIZE / 2 + 4);
                Canvas.SetTop(_selectedNode.LabelVisual!, _selectedNode.Y - 8);
                UpdateAllEdges();
            }
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Min(3.0, _zoom + 0.2);
            GraphCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Max(0.3, _zoom - 0.2);
            GraphCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        }

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            _zoom = 1.0;
            GraphCanvas.LayoutTransform = new ScaleTransform(1.0, 1.0);
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
            HelpPopup.PlacementTarget = HelpButton;
        }
        public async Task HighlightClassAsync(string className, string projectPath)
        {
            System.Diagnostics.Debug.WriteLine($"HighlightClassAsync: className={className}, projectPath={projectPath}");
            System.Diagnostics.Debug.WriteLine($"Directory.Exists: {Directory.Exists(projectPath)}");

            GraphBorder.Visibility = Visibility.Visible;
            InfoPanel.Visibility = Visibility.Visible;
            HintText.Visibility = Visibility.Collapsed;

            if (_nodes.Count == 0 && !string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
            {
                System.Diagnostics.Debug.WriteLine("Строим граф...");
                await BuildGraphAsync(projectPath);
                System.Diagnostics.Debug.WriteLine($"Построено узлов: {_nodes.Count}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Граф не построен: nodes={_nodes.Count}, path={projectPath}, exists={Directory.Exists(projectPath)}");
            }

            var node = _nodes.FirstOrDefault(n =>
                n.Name.Equals(className, StringComparison.OrdinalIgnoreCase) ||
                n.FullName.EndsWith("." + className, StringComparison.OrdinalIgnoreCase));

            if (node != null)
            {
                node.HasViolation = true;
                RedrawNode(node);
                SelectNode(node);
                ScrollToNode(node);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Узел не найден: {className}. Доступные: {string.Join(", ", _nodes.Select(n => n.Name))}");
            }
        }

        private void ScrollToNode(GraphNodeVisual node)
        {
            var scrollViewer = GraphCanvas.Parent as ScrollViewer;
            if (scrollViewer == null) return;

            GraphCanvas.UpdateLayout();
            scrollViewer.UpdateLayout();

            double targetX = Math.Max(0, node.X - scrollViewer.ViewportWidth / 2);
            double targetY = Math.Max(0, node.Y - scrollViewer.ViewportHeight / 2);

            scrollViewer.ScrollToHorizontalOffset(targetX);
            scrollViewer.ScrollToVerticalOffset(targetY);
        }
    }

    public class GraphNodeVisual
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Kind { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public bool HasViolation { get; set; }
        public string FilePath { get; set; } = "";
        public int LineNumber { get; set; }
        public int MethodCount { get; set; }
        public double Complexity { get; set; }
        public bool IsSelected { get; set; }
        public Ellipse? DotVisual { get; set; }
        public TextBlock? LabelVisual { get; set; }
        public string Category { get; set; } = "Other";
    }

    public class GraphEdgeVisual
    {
        public GraphNodeVisual Source { get; set; } = null!;
        public GraphNodeVisual Target { get; set; } = null!;
        public string RelationType { get; set; } = "";
        public bool IsViolation { get; set; }
        public Line? Visual { get; set; }
    }
}