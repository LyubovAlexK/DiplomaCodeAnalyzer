using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class ResultsPage : UserControl
    {
        private List<Core.Models.SessionAnalysis> _sessions = new();
        private List<VerdictRow> _allVerdicts = new();
        private bool _isLoaded;

        public ResultsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadTraineesAsync();
        }

        private async System.Threading.Tasks.Task LoadTraineesAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var trainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();

                TraineeCombo.Items.Clear();
                foreach (var t in trainees)
                    TraineeCombo.Items.Add(new ComboBoxItem { Content = $"{t.LastName} {t.FirstName}".Trim(), Tag = t.UserId });

                if (trainees.Any())
                {
                    TraineeCombo.SelectedIndex = 0;
                    _isLoaded = true;
                }
            }
            catch { }
        }

        private async void TraineeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || TraineeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int traineeId)
                return;
            await LoadDataAsync(traineeId);
        }

        private async System.Threading.Tasks.Task LoadDataAsync(int traineeId)
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                _sessions = await db.SessionAnalysis
                    .Where(s => s.TraineeId == traineeId && s.IsArchived != true)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();

                if (!_sessions.Any())
                {
                    PlaceholderText.Text = "Стажёр ещё не выполнял проверки";
                    PlaceholderText.Visibility = Visibility.Visible;
                    KpiGrid.Visibility = Visibility.Collapsed;
                    SessionsPanel.Visibility = Visibility.Collapsed;
                    VerdictsPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                PlaceholderText.Visibility = Visibility.Collapsed;
                KpiGrid.Visibility = Visibility.Visible;
                SessionsPanel.Visibility = Visibility.Visible;

                AttemptsText.Text = _sessions.Count.ToString();
                LastPercentText.Text = $"{_sessions.First().OverallMatchPercent:F0}%";

                int roslynFail = 0, archFail = 0;
                foreach (var s in _sessions)
                {
                    var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == s.SessionId).ToListAsync();
                    roslynFail += verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                    archFail += verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                }
                SyntaxFailText.Text = roslynFail.ToString();
                ArchFailText.Text = archFail.ToString();

                var projectIds = _sessions.Select(s => s.ProjectId).Distinct().ToList();
                var projects = await db.Projects.Where(p => projectIds.Contains(p.ProjectId)).ToListAsync();

                SessionsGrid.ItemsSource = _sessions.Select(s =>
                {
                    var project = projects.FirstOrDefault(p => p.ProjectId == s.ProjectId);
                    string statusText = s.Status switch
                    {
                        "Completed" => "Завершена",
                        "InProgress" => "В процессе",
                        _ => s.Status ?? "—"
                    };
                    return new
                    {
                        Project = project?.Title ?? "—",
                        Date = s.StartTime?.ToString("dd.MM.yyyy HH:mm") ?? "—",
                        Percent = $"{s.OverallMatchPercent:F0}%",
                        Status = statusText,
                        SessionId = s.SessionId
                    };
                }).ToList();

                if (_sessions.Any())
                    SessionsGrid.SelectedIndex = 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private async void SessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionsGrid.SelectedItem == null)
            {
                VerdictsPanel.Visibility = Visibility.Collapsed;
                return;
            }
            dynamic row = SessionsGrid.SelectedItem;
            int sessionId = row.SessionId;
            await LoadVerdictsAsync(sessionId);
        }

        private async System.Threading.Tasks.Task LoadVerdictsAsync(int sessionId)
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == sessionId).ToListAsync();

                _allVerdicts = verdicts.Select(v =>
                {
                    string aiModel = v.AiModel switch
                    {
                        "Roslyn" => "Синтаксис",
                        "NetArchTest" => "Архитектура",
                        "GigaChat" => "Семантическая",
                        _ => v.AiModel ?? "—"
                    };
                    if (VerdictsGrid != null)
                        ApplyVerdictFilter();

                    return new VerdictRow
                    {
                        AiModel = aiModel,
                        Reason = v.Reason ?? "—",
                        CodeLocation = v.CodeLocation ?? "—",
                        TypeText = aiModel,
                        TypeBg = aiModel switch
                        {
                            "Синтаксис" => "#DBEAFE",
                            "Архитектура" => "#FEF3C7",
                            "Семантическая" => "#DDD6FE",
                            _ => "#F3F4F6"
                        },
                        TypeFg = aiModel switch
                        {
                            "Синтаксис" => "#2563EB",
                            "Архитектура" => "#D97706",
                            "Семантическая" => "#7C3AED",
                            _ => "#6B7280"
                        }
                    };
                }).ToList();

                VerdictsPanel.Visibility = _allVerdicts.Any() ? Visibility.Visible : Visibility.Collapsed;
                ApplyVerdictFilter();
            }
            catch { }
        }

        private void ApplyVerdictFilter()
        {
            if (VerdictsGrid == null || VerdictSearchBox == null) return;

            var filtered = _allVerdicts.AsEnumerable();
            if (VerdictTypeCombo?.SelectedItem is ComboBoxItem typeItem)
            {
                string tag = typeItem.Tag?.ToString() ?? "all";
                if (tag != "all") filtered = filtered.Where(v => v.AiModel == tag);
            }
            string search = VerdictSearchBox?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(v =>
                    (v.AiModel ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (v.Reason ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            VerdictsGrid.ItemsSource = filtered.ToList();
        }

        private void VerdictTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyVerdictFilter();
        private void VerdictSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyVerdictFilter();

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (TraineeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int traineeId)
            {
                MessageBox.Show("Выберите стажёра");
                return;
            }

            if (SessionsGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите сессию для экспорта");
                return;
            }

            dynamic row = SessionsGrid.SelectedItem;
            int sessionId = row.SessionId;

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var db = App.GetService<AppDbContext>();
                var session = await db.SessionAnalysis.FindAsync(sessionId);
                var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == sessionId).ToListAsync();
                var trainee = await db.Users.FindAsync(traineeId);
                var project = await db.Projects.FindAsync(session?.ProjectId);

                var traineeName = trainee != null ? $"{trainee.LastName} {trainee.FirstName}".Trim() : "";

                var dialog = new SaveFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    FileName = $"Отчёт_{traineeName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (dialog.ShowDialog() != true) return;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Inter"));

                        page.Header().Text($"Отчёт по проверке")
                            .FontSize(20).Bold().AlignCenter();

                        page.Content().Column(col =>
                        {
                            col.Item().Text($"Стажёр: {traineeName}").Bold();
                            col.Item().Text($"Проект: {project?.Title ?? "—"}");
                            col.Item().Text($"Дата: {session?.StartTime:dd.MM.yyyy HH:mm}");
                            col.Item().Text($"Соответствие: {session?.OverallMatchPercent:F0}%");
                            col.Item().Text("");

                            col.Item().Text("Нарушения:").Bold();
                            foreach (var v in verdicts)
                            {
                                col.Item().Text($"[{v.AiModel}] {v.Reason} — {v.CodeLocation ?? "—"}");
                            }
                        });
                    });
                }).GeneratePdf(dialog.FileName);

                MessageBox.Show("Отчёт сохранён!", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }
    }

    public class VerdictRow
    {
        public string AiModel { get; set; } = "";
        public string Reason { get; set; } = "";
        public string CodeLocation { get; set; } = "";
        public string TypeText { get; set; } = "";
        public string TypeBg { get; set; } = "";
        public string TypeFg { get; set; } = "";
    }
}