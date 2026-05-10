using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class TraineeHomePage : UserControl
    {
        public TraineeHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
            SizeChanged += OnSizeChanged;
        }
        private bool _chartDrawn;
        private List<Core.Models.SessionAnalysis> _cachedSessions = new();
        private List<Core.Models.SessionAnalysis> _allSessions = new();
        private int _selectedProjectId;
        private List<Rectangle> _bars = new();
        private List<Core.Models.SessionAnalysis> _barSessions = new();

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_chartDrawn)
            {
                DrawProgressChartForProject(_selectedProjectId);
            }
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;

                var projects = await db.Projects
                    .Where(p => p.TraineeId == myId && p.IsArchived != true)
                    .ToListAsync();

                var sessions = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.IsArchived != true)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();

                _allSessions = sessions;
                _cachedSessions = sessions;

                // Заполняем комбобокс проектов для графика
                ProjectChartCombo.Items.Clear();
                foreach (var proj in projects)
                    ProjectChartCombo.Items.Add(new ComboBoxItem { Content = proj.Title, Tag = proj.ProjectId });

                if (projects.Any())
                {
                    ProjectChartCombo.SelectedIndex = 0;
                    _selectedProjectId = projects.First().ProjectId;
                }

                // === KPI ===
                MyProjectsText.Text = projects.Count.ToString();
                TotalAttemptsText.Text = sessions.Count.ToString();
                BestResultText.Text = sessions.Any()
                    ? $"{sessions.Max(s => s.OverallMatchPercent ?? 0):F1}%"
                    : "—";
                CompletedText.Text = projects.Count(p => p.IsCompleted == true).ToString();

                // === Активные проекты ===
                ActiveProjectsPanel.Children.Clear();
                foreach (var proj in projects)
                {
                    var projSessions = sessions.Where(s => s.ProjectId == proj.ProjectId).ToList();
                    var lastSession = projSessions.LastOrDefault();
                    double pct = (double)(lastSession?.OverallMatchPercent ?? 0);
                    double fullWidth = 300;

                    var card = new Border
                    {
                        Style = (Style)FindResource("CardBorder"),
                        Margin = new Thickness(0, 0, 0, 10),
                        Padding = new Thickness(16)
                    };
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = proj.Title,
                        FontSize = (double)FindResource("AppFontSizeH3"),
                        FontFamily = new FontFamily("Inter"),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("DarkTextBrush")
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = proj.IsCompleted == true ? "Пройден" : "В процессе",
                        FontSize = (double)FindResource("AppFontSizeH4"),
                        FontFamily = new FontFamily("Inter"),
                        Foreground = proj.IsCompleted == true ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("WarningBrush"),
                        Margin = new Thickness(0, 4, 0, 8)
                    });

                    Brush barColor = pct >= 80 ? (Brush)FindResource("SuccessBrush") :
                                     pct >= 50 ? (Brush)FindResource("PrimaryBrush") :
                                     pct > 0 ? (Brush)FindResource("WarningBrush") : (Brush)FindResource("DangerBrush");

                    var progressBg = new Border
                    {
                        Height = 8,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F6")),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, 6),
                        Width = fullWidth
                    };
                    var fill = new Border
                    {
                        Height = 8,
                        Width = pct / 100.0 * fullWidth,
                        Background = barColor,
                        CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    progressBg.Child = fill;
                    stack.Children.Add(progressBg);

                    stack.Children.Add(new TextBlock
                    {
                        Text = $"Попыток: {projSessions.Count}   Прогресс: {pct:F0}%",
                        FontSize = (double)FindResource("AppFontSizeH4"),
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    });
                    card.Child = stack;
                    card.MouseLeftButtonDown += (s, e) =>
                    {
                        var mainWindow = Window.GetWindow(this) as MainWindow;
                        if (mainWindow != null)
                        {
                            var resultsPage = new MyResultsPage();
                            // Передаём ID проекта через свойство
                            resultsPage.SetProjectId(proj.ProjectId);
                            mainWindow.ContentArea.Content = resultsPage;
                            mainWindow.SetActiveButton("MyResults");
                        }
                    };
                    card.Cursor = Cursors.Hand;
                    ActiveProjectsPanel.Children.Add(card);
                }

                // === График прогресса ===
                DrawProgressChartForProject(_selectedProjectId);

                // === Gauge + легенда (по выбранному проекту) ===
                var projectSessions = sessions.Where(s => s.ProjectId == _selectedProjectId).ToList();
                var last = projectSessions.LastOrDefault();
                if (last != null)
                {
                    var verdicts = await db.AuditVerdicts
                        .Where(v => v.SessionId == last.SessionId)
                        .ToListAsync();

                    double pct = (double)(last.OverallMatchPercent ?? 0);
                    int roslynFail = verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                    int archFail = verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                    int aiFail = verdicts.Count(v => v.AiModel == "GigaChat" && !v.IsPassed);

                    int refIssues = 0;
                    var refProjects = await db.ReferenceProjects
                        .Where(rp => rp.SpecificationId == last.SpecificationId && rp.IsActive)
                        .FirstOrDefaultAsync();
                    if (refProjects != null)
                    {
                        var refService = App.GetService<Analyzers.Services.ReferenceService>();
                        var roslynResult = new Core.Models.CodeAnalysisResult();
                        var refResult = await refService.CompareWithReference(refProjects.ReferenceId, roslynResult);
                        refIssues = refResult.Count;
                    }

                    DrawGaugeChart(pct);
                    GaugePercentText.Text = $"{pct:F0}%";

                    LegendPanel2.Children.Clear();
                    AddLegendItem(LegendPanel2, "Синтаксис", $"{roslynFail} нарушений", roslynFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "Архитектура", $"{archFail} нарушений", archFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "AI Judge", $"{aiFail} не пройдено", aiFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "Эталон", $"{refIssues} расхождений", refIssues > 0 ? "#F59E0B" : "#10B981");
                }
                else
                {
                    GaugeCanvas.Children.Clear();
                    GaugePercentText.Text = "—";
                    LegendPanel2.Children.Clear();
                    LegendPanel2.Children.Add(new TextBlock
                    {
                        Text = "Нет данных",
                        FontSize = (double)FindResource("AppFontSizeH4"),
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void DrawGaugeChart(double percent)
        {
            GaugeCanvas.Children.Clear();
            double cx = 60, cy = 60, r = 50, thickness = 14;
            double totalSweep = 360;

            // Серый фон (полный круг)
            for (double a = 0; a < totalSweep; a += 0.5)
            {
                double rad = a * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy + (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy + r * Math.Sin(rad),
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F6")),
                    StrokeThickness = 2.2
                });
            }

            // Цветной сегмент (полный круг, процент = угол)
            double sweep = percent / 100.0 * totalSweep;
            string colorHex = percent >= 80 ? "#10B981" : percent >= 50 ? "#F59E0B" : "#EF4444";
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            for (double a = 0; a < sweep; a += 0.5)
            {
                double rad = a * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy + (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy + r * Math.Sin(rad),
                    Stroke = brush,
                    StrokeThickness = 2.2
                });
            }

            // Белый круг внутри (пончик)
            var hole = new Ellipse
            {
                Width = (r - thickness) * 2,
                Height = (r - thickness) * 2,
                Fill = Brushes.White
            };
            Canvas.SetLeft(hole, cx - (r - thickness));
            Canvas.SetTop(hole, cy - (r - thickness));
            GaugeCanvas.Children.Add(hole);
        }

        private void AddLegendItem(Panel panel, string label, string value, string colorHex)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            stack.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                Margin = new Thickness(0, 0, 10, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{label}: {value}",
                FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush")
            });
            panel.Children.Add(stack);
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null) mainWindow.ContentArea.Content = new MyResultsPage();
            mainWindow.SetActiveButton("MyResults");
        }

        private void DrawProgressChart(List<Core.Models.SessionAnalysis> sessions)
        {
            ProgressCanvas.Children.Clear();
            _bars.Clear();
            _barSessions = sessions;

            if (!sessions.Any()) return;

            double canvasW = ProgressCanvas.ActualWidth > 0 ? ProgressCanvas.ActualWidth : 500;
            double canvasH = ProgressCanvas.ActualHeight > 0 ? ProgressCanvas.ActualHeight : 220;
            double pad = 40;
            double chartW = canvasW - pad * 2;
            double chartH = canvasH - pad - 20;
            double maxVal = 100;
            double barWidth = Math.Max(12, chartW / sessions.Count * 0.6);
            double gap = chartW / sessions.Count;

            // Сетка
            for (int i = 0; i <= 4; i++)
            {
                double y = pad + (chartH / 4) * i;
                ProgressCanvas.Children.Add(new Line
                {
                    X1 = pad,
                    Y1 = y,
                    X2 = canvasW - pad,
                    Y2 = y,
                    Stroke = (Brush)FindResource("BorderBrush"),
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 4.0 })
                });
                var lbl = new TextBlock
                {
                    Text = $"{100 - i * 25}%",
                    FontSize = (double)FindResource("AppFontSizeH3"),
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(lbl, 2); Canvas.SetTop(lbl, y - 8);
                ProgressCanvas.Children.Add(lbl);
            }

            // Порог сдачи (80%) — рисуем ПОСЛЕ сетки, ПЕРЕД столбцами
            double passLineY = pad + chartH - (80.0 / maxVal) * chartH;
            ProgressCanvas.Children.Add(new Line
            {
                X1 = pad,
                Y1 = passLineY,
                X2 = canvasW - pad,
                Y2 = passLineY,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 })
            });
            var passLbl = new TextBlock
            {
                Text = "80%",
                FontSize = 9,
                FontFamily = new FontFamily("Inter"),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
            };
            Canvas.SetLeft(passLbl, canvasW - pad + 5);
            Canvas.SetTop(passLbl, passLineY - 8);
            ProgressCanvas.Children.Add(passLbl);

            double bestPct = (double)sessions.Max(s => s.OverallMatchPercent ?? 0);
            int step = sessions.Count > 20 ? 5 : sessions.Count > 8 ? 2 : 1;

            for (int i = 0; i < sessions.Count; i++)
            {
                double pct = (double)(sessions[i].OverallMatchPercent ?? 0);
                double x = pad + gap * i + (gap - barWidth) / 2;
                double h = Math.Max(2, pct / maxVal * chartH);
                double y = pad + chartH - h;

                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = h,
                    Fill = pct == bestPct
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                        : (Brush)FindResource("PrimaryBrush"),
                    RadiusX = 4,
                    RadiusY = 4,
                    Tag = i,
                    Cursor = Cursors.Hand
                };
                Canvas.SetLeft(bar, x); Canvas.SetTop(bar, y);
                Canvas.SetZIndex(bar, 1); // столбцы поверх порога
                ProgressCanvas.Children.Add(bar);
                _bars.Add(bar);

                if (i % step == 0 || i == sessions.Count - 1)
                {
                    var xlbl = new TextBlock
                    {
                        Text = $"{pct:F0}%",
                        FontSize = (double)FindResource("AppFontSizeH3"),
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    };
                    Canvas.SetLeft(xlbl, x + barWidth / 2 - 12);
                    Canvas.SetTop(xlbl, canvasH - 18);
                    Canvas.SetZIndex(xlbl, 2);
                    ProgressCanvas.Children.Add(xlbl);
                }
            }

            BestResultLabel.Text = $"Лучший: {bestPct:F0}%";
        }

        private void ProgressCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ProgressCanvas);
            foreach (var bar in _bars)
            {
                double left = Canvas.GetLeft(bar);
                double top = Canvas.GetTop(bar);
                if (pos.X >= left && pos.X <= left + bar.Width && pos.Y >= top && pos.Y <= top + bar.Height)
                {
                    int idx = (int)bar.Tag;
                    var session = _barSessions[idx];
                    BarTooltip.Visibility = Visibility.Visible;
                    BarTooltipAttempt.Text = $"Попытка #{idx + 1}";
                    BarTooltipValue.Text = $"{session.OverallMatchPercent ?? 0:F0}%";
                    Canvas.SetLeft(BarTooltip, left + bar.Width / 2 - 40);
                    Canvas.SetTop(BarTooltip, top - 35);
                    bar.Opacity = 0.8;
                    return;
                }
                bar.Opacity = 1.0;
            }
            BarTooltip.Visibility = Visibility.Collapsed;
        }

        private void ProgressCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            BarTooltip.Visibility = Visibility.Collapsed;
            foreach (var bar in _bars) bar.Opacity = 1.0;
        }

        private async void ProjectChartCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectChartCombo.SelectedItem is ComboBoxItem item && item.Tag is int projId)
            {
                _selectedProjectId = projId;
                DrawProgressChartForProject(projId);

                // Обновляем Gauge для выбранного проекта
                var projectSessions = _allSessions.Where(s => s.ProjectId == projId).ToList();
                var last = projectSessions.LastOrDefault();
                if (last != null)
                {
                    var db = App.GetService<AppDbContext>();
                    var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == last.SessionId).ToListAsync();
                    double pct = (double)(last.OverallMatchPercent ?? 0);
                    int roslynFail = verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                    int archFail = verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                    int aiFail = verdicts.Count(v => v.AiModel == "GigaChat" && !v.IsPassed);

                    DrawGaugeChart(pct);
                    GaugePercentText.Text = $"{pct:F0}%";

                    LegendPanel2.Children.Clear();
                    AddLegendItem(LegendPanel2, "Синтаксис", $"{roslynFail} нарушений", roslynFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "Архитектура", $"{archFail} нарушений", archFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "AI Judge", $"{aiFail} не пройдено", aiFail > 0 ? "#EF4444" : "#10B981");
                }
            }
        }
        private void DrawProgressChartForProject(int projectId)
        {
            var sessions = _allSessions
                .Where(s => s.ProjectId == projectId)
                .OrderBy(s => s.StartTime)
                .TakeLast(10)
                .ToList();

            DrawProgressChart(sessions);
        }
    }
}