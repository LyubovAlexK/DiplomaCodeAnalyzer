using Analyzers.Services;
using Core.Data;
using System.IO;
using Core.Models;
using Core.Services;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class MyResultsPage : UserControl
    {
        private List<VerdictRow> _allVerdicts = new();
        private bool _isLoaded;
        private string? _lastProjectPath;
        public MyResultsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadProjectsAsync();
        }
        private int? _forcedProjectId;
        public void SetProjectId(int projectId)
        {
            _forcedProjectId = projectId;
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

                ProjectFilterCombo.SelectionChanged -= ProjectFilterCombo_SelectionChanged;
                ProjectFilterCombo.Items.Clear();

                foreach (var p in projects)
                    ProjectFilterCombo.Items.Add(new ComboBoxItem { Content = p.Title, Tag = p.ProjectId });

                if (_forcedProjectId.HasValue)
                {
                    for (int i = 0; i < ProjectFilterCombo.Items.Count; i++)
                    {
                        if (ProjectFilterCombo.Items[i] is ComboBoxItem item && item.Tag is int id && id == _forcedProjectId.Value)
                        {
                            ProjectFilterCombo.SelectedIndex = i;
                            _forcedProjectId = null;
                            break;
                        }
                    }
                }
                else if (ProjectFilterCombo.Items.Count > 0)
                {
                    ProjectFilterCombo.SelectedIndex = 0;
                }

                _isLoaded = true;
                ProjectFilterCombo.SelectionChanged += ProjectFilterCombo_SelectionChanged;

                // Явная первая загрузка
                if (ProjectFilterCombo.SelectedItem is ComboBoxItem selected && selected.Tag is int projId)
                    await LoadResultsAsync(projId);
            }
            catch { }
        }

        private async void ProjectFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || e.AddedItems.Count == 0) return;
            if (ProjectFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
            {
                HideAllPanels();
                PlaceholderText.Visibility = Visibility.Visible;
                PlaceholderText.Text = "Загрузка...";
                await LoadResultsAsync(projectId);
            }
        }

        private async Task LoadResultsAsync(int projectId)
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;

                var lastSession = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.ProjectId == projectId && s.IsArchived != true)
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefaultAsync();

                var project = await db.Projects.FindAsync(projectId);
                _lastProjectPath = project?.RepoUrl ?? "";

                if (lastSession == null)
                {
                    PlaceholderText.Text = "Нет завершённых проверок для этого проекта";
                    PlaceholderText.Visibility = Visibility.Visible;
                    HideAllPanels();
                    return;
                }

                PlaceholderText.Visibility = Visibility.Collapsed;

                var verdicts = await db.AuditVerdicts
                    .Where(v => v.SessionId == lastSession.SessionId)
                    .ToListAsync();

                double pct = (double)(lastSession.OverallMatchPercent ?? 0);
                int roslynFail = verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                int archFail = verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);

                MatchPercentText.Text = $"{pct:F0}%";
                RoslynFailText.Text = roslynFail.ToString();
                ArchFailText.Text = archFail.ToString();
                RefMatchText.Text = lastSession.ReferenceMatchPercent.HasValue
                    ? $"{lastSession.ReferenceMatchPercent:F0}%"
                    : "—";

                _allVerdicts = verdicts.Select(v =>
                {
                    string aiModel = v.AiModel switch
                    {
                        "Roslyn" => "Синтаксис",
                        "NetArchTest" => "Архитектура",
                        "GigaChat" => "Семантика",
                        _ => v.AiModel ?? "—"
                    };

                    return new VerdictRow
                    {
                        AiModel = aiModel,
                        Reason = v.Reason ?? "—",
                        CodeLocation = v.CodeLocation ?? "—",
                        CreatedAt = v.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        TypeText = aiModel,
                        TypeBg = aiModel switch
                        {
                            "Синтаксис" => "#DBEAFE",
                            "Архитектура" => "#FEF3C7",
                            "Семантика" => "#DDD6FE",
                            _ => "#F3F4F6"
                        },
                        TypeFg = aiModel switch
                        {
                            "Синтаксис" => "#2563EB",
                            "Архитектура" => "#D97706",
                            "Семантика" => "#7C3AED",
                            _ => "#6B7280"
                        },
                        IsSelected = false
                    };
                }).ToList();

                ApplyVerdictFilter();

                UpdateSendStatus(pct);
                ShowAllPanels();

                var reference = await db.ReferenceProjects
                    .FirstOrDefaultAsync(r => r.SpecificationId == lastSession.SpecificationId);
                if (reference != null)
                {
                    var refService = App.GetService<ReferenceService>();
                    var roslynResult = new CodeAnalysisResult
                    {
                        TotalMethods = verdicts.Count(v => v.AiModel == "Roslyn"),
                        MethodsExceedingComplexity = roslynFail
                    };
                    var refResult = await refService.CompareWithReference(reference.ReferenceId, roslynResult);
                    ReferenceText.Text = $"Соответствие эталону: {lastSession.ReferenceMatchPercent:F0}%\n" +
                        $"Расхождений: {refResult.Count}";
                }
                else
                {
                    ReferenceText.Text = "Эталон не задан";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void UpdateSendStatus(double percent)
        {
            if (percent >= 80)
            {
                StatusDot.Fill = (Brush)FindResource("SuccessBrush");
                StatusText.Text = $"Проект готов к отправке ({percent:F0}%)";
                StatusText.Foreground = (Brush)FindResource("SuccessBrush");
                SendToMentorBtn.IsEnabled = true;
                SendToMentorBtn.Opacity = 1.0;
            }
            else if (percent >= 50)
            {
                StatusDot.Fill = (Brush)FindResource("WarningBrush");
                StatusText.Text = $"Требует доработки ({percent:F0}%)";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                SendToMentorBtn.IsEnabled = true;
                SendToMentorBtn.Opacity = 0.7;
            }
            else
            {
                StatusDot.Fill = (Brush)FindResource("DangerBrush");
                StatusText.Text = $"Не готово к отправке ({percent:F0}%)";
                StatusText.Foreground = (Brush)FindResource("DangerBrush");
                SendToMentorBtn.IsEnabled = false;
                SendToMentorBtn.Opacity = 0.5;
            }
        }

        private void ApplyVerdictFilter()
        {
            if (VerdictSearchBox == null) return;
            var filtered = _allVerdicts.AsEnumerable();

            if (VerdictTypeCombo?.SelectedItem is ComboBoxItem typeItem)
            {
                string typeTag = typeItem.Tag?.ToString() ?? "all";
                if (typeTag != "all") filtered = filtered.Where(v => v.AiModel == typeTag);
            }
            string search = VerdictSearchBox?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(v =>
                    (v.AiModel ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (v.Reason ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (v.CodeLocation ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            VerdictsGrid.ItemsSource = filtered.ToList();
        }

        private void VerdictTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyVerdictFilter();
        private void VerdictFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyVerdictFilter();
        private void VerdictSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyVerdictFilter();

        private void HideAllPanels()
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            KpiGrid.Visibility = Visibility.Collapsed;
            VerdictsPanel.Visibility = Visibility.Collapsed;
            ReferencePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowAllPanels()
        {
            StatusPanel.Visibility = Visibility.Visible;
            KpiGrid.Visibility = Visibility.Visible;
            VerdictsPanel.Visibility = Visibility.Visible;
            ReferencePanel.Visibility = Visibility.Visible;
        }

        private void VerdictsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VerdictsGrid.SelectedItem is VerdictRow v)
            {
                string? className = null;
                if (!string.IsNullOrEmpty(v.CodeLocation) && v.CodeLocation.Contains(':'))
                {
                    var lastColonIndex = v.CodeLocation.LastIndexOf(':');
                    if (lastColonIndex >= 0 && lastColonIndex < v.CodeLocation.Length - 1)
                    {
                        var classAndMethod = v.CodeLocation.Substring(lastColonIndex + 1);
                        className = classAndMethod.Split('.').FirstOrDefault();
                    }
                }

                if (!string.IsNullOrEmpty(className))
                {
                    if (MessageBox.Show($"Перейти к графу и подсветить класс «{className}»?",
                        "Открыть граф", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        var mainWindow = Window.GetWindow(this) as MainWindow;
                        if (mainWindow != null)
                        {
                            // Получаем путь из БД
                            var db = App.GetService<AppDbContext>();
                            int projectId = (int)((ProjectFilterCombo.SelectedItem as ComboBoxItem)?.Tag ?? 2);
                            var project = db.Projects.Find(projectId);
                            string projectPath = project?.RepoUrl ?? "";

                            var codePage = new MyCodePage();
                            mainWindow.ContentArea.Content = codePage;
                            mainWindow.SetActiveButton("MyCode");
                            _ = codePage.HighlightClassAsync(className, projectPath);
                        }
                    }
                }
            }
        }

        private async void SendToMentor_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectFilterCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int projectId)
                return;

            var db = App.GetService<AppDbContext>();
            var project = await db.Projects.FindAsync(projectId);
            if (project == null) return;

            int myId = App.CurrentUser?.UserId ?? 0;

            var lastSession = await db.SessionAnalysis
                .Where(s => s.TraineeId == myId && s.ProjectId == projectId && s.IsArchived != true)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync();

            double pct = (double)(lastSession?.OverallMatchPercent ?? 0);
            if (pct < 80)
            {
                MessageBox.Show($"Проект ещё не готов к отправке. Текущий процент: {pct:F0}%. Требуется минимум 80%.",
                    "Не готово", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            project.IsCompleted = true;
            project.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();

            MessageBox.Show("Проект успешно отправлен наставнику на проверку!",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

            UpdateSendStatus(pct);
        }

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProjectFilterCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int projectId)
                    return;

                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;

                var session = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.ProjectId == projectId && s.IsArchived != true)
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    MessageBox.Show("Нет данных для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == session.SessionId).ToListAsync();
                var user = await db.Users.FindAsync(myId);
                var project = await db.Projects.FindAsync(projectId);

                var pdf = ReportService.GenerateSessionReport(session, verdicts, user?.FullName ?? "", project?.Title ?? "");

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    FileName = $"Отчёт_{project?.Title}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(dialog.FileName, pdf);
                    MessageBox.Show("Отчёт сохранён!", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class VerdictRow
    {
        public string AiModel { get; set; } = "";
        public string Reason { get; set; } = "";
        public string CodeLocation { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string TypeText { get; set; } = "";
        public string TypeBg { get; set; } = "";
        public string TypeFg { get; set; } = "";
        public bool IsSelected { get; set; }
    }
}