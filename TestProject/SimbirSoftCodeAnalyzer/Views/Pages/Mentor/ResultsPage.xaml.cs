using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
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
                TraineeCombo.Items.Add(new ComboBoxItem { Content = "Все стажёры", Tag = 0 });
                foreach (var t in trainees)
                    TraineeCombo.Items.Add(new ComboBoxItem { Content = $"{t.LastName} {t.FirstName}".Trim(), Tag = t.UserId });

                if (!trainees.Any())
                {
                    PlaceholderText.Text = "Нет зарегистрированных стажёров";
                    PlaceholderText.Visibility = Visibility.Visible;
                    SummaryPanel.Visibility = Visibility.Collapsed;
                    _isLoaded = true;
                    return;
                }

                TraineeCombo.SelectedIndex = 0;
                _isLoaded = true;

                await LoadSummaryAsync();
                SummaryPanel.Visibility = Visibility.Collapsed;
                KpiGrid.Visibility = Visibility.Collapsed;
                SessionsPanel.Visibility = Visibility.Collapsed;
                VerdictsPanel.Visibility = Visibility.Collapsed;
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private async void TraineeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || TraineeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int selectedId)
                return;

            if (selectedId == 0)
            {
                PlaceholderText.Visibility = Visibility.Collapsed;
                KpiGrid.Visibility = Visibility.Collapsed;
                SessionsPanel.Visibility = Visibility.Collapsed;
                VerdictsPanel.Visibility = Visibility.Collapsed;
                SummaryPanel.Visibility = Visibility.Visible;
                await LoadSummaryAsync();
                return;
            }

            await LoadDataAsync(selectedId);
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

        private async System.Threading.Tasks.Task LoadSummaryAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var trainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();
                var allSessions = await db.SessionAnalysis.Where(s => s.IsArchived != true).ToListAsync();
                var projects = await db.Projects.Where(p => p.IsArchived != true).ToListAsync();

                var rows = new List<dynamic>();
                foreach (var t in trainees)
                {
                    var sessions = allSessions.Where(s => s.TraineeId == t.UserId).OrderBy(s => s.StartTime).ToList();
                    var last = sessions.LastOrDefault();
                    var proj = projects.FirstOrDefault(p => p.TraineeId == t.UserId);
                    double lastPct = (double)(last?.OverallMatchPercent ?? 0);
                    double avgPct = sessions.Any() ? sessions.Average(s => (double)(s.OverallMatchPercent ?? 0)) : 0;

                    int syntaxFails = 0, archFails = 0;
                    foreach (var s in sessions)
                    {
                        var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == s.SessionId).ToListAsync();
                        syntaxFails += verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                        archFails += verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                    }

                    string statusText = last == null ? "Нет попыток" : lastPct >= 80 ? "Готов" : lastPct >= 50 ? "Доработка" : "Не готов";
                    string statusBg = last == null ? "#F3F4F6" : lastPct >= 80 ? "#D1FAE5" : lastPct >= 50 ? "#FEF3C7" : "#FEE2E2";
                    string statusFg = last == null ? "#6B7280" : lastPct >= 80 ? "#10B981" : lastPct >= 50 ? "#D97706" : "#EF4444";

                    rows.Add(new
                    {
                        Name = $"{t.LastName} {t.FirstName}".Trim(),
                        Project = proj?.Title ?? "—",
                        Attempts = sessions.Count,
                        AvgPercent = sessions.Any() ? $"{avgPct:F0}%" : "—",
                        LastPercent = last != null ? $"{lastPct:F0}%" : "—",
                        SyntaxFails = syntaxFails,
                        ArchFails = archFails,
                        StatusText = statusText,
                        StatusBg = statusBg,
                        StatusFg = statusFg
                    });
                }
                SummaryGrid.ItemsSource = rows;
            }
            catch { }
        }

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
                        page.Margin(25);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Inter"));

                        page.Header().Text($"Отчёт по проверке")
                            .FontSize(20).Bold().AlignCenter();

                        page.Content().Column(col =>
                        {
                            col.Item().Text("");
                            col.Item().Text($"Стажёр: {traineeName}").FontSize(14).Bold();
                            col.Item().Text($"Проект: {project?.Title ?? "—"}");
                            col.Item().Text($"Дата проверки: {session?.StartTime:dd.MM.yyyy HH:mm}");
                            col.Item().Text($"Процент соответствия ТЗ: {session?.OverallMatchPercent:F0}%");
                            col.Item().Text($"Процент соответствия эталону: {session?.ReferenceMatchPercent:F0}%");
                            col.Item().Text($"Статус: {(session?.Status == "Completed" ? "Завершена" : session?.Status ?? "—")}");
                            col.Item().Text("");

                            if (verdicts.Any())
                            {
                                col.Item().Text($"Нарушения ({verdicts.Count}):").FontSize(12).Bold();
                                col.Item().Text("");

                                col.Item().Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c =>
                                    {
                                        c.ConstantColumn(80);
                                        c.RelativeColumn(3);
                                        c.RelativeColumn(2);
                                    });

                                    tbl.Header(h =>
                                    {
                                        h.Cell().Background("#F1F5F9").Padding(4).Text("Тип").FontSize(9).Bold();
                                        h.Cell().Background("#F1F5F9").Padding(4).Text("Описание").FontSize(9).Bold();
                                        h.Cell().Background("#F1F5F9").Padding(4).Text("Расположение").FontSize(9).Bold();
                                    });

                                    foreach (var v in verdicts)
                                    {
                                        string typeText = v.AiModel switch
                                        {
                                            "Roslyn" => "Синтаксис",
                                            "NetArchTest" => "Архитектура",
                                            "GigaChat" => "Семантика",
                                            "Reference" => "Эталон",
                                            _ => v.AiModel ?? "—"
                                        };

                                        tbl.Cell().Padding(4).Text(typeText).FontSize(9);
                                        tbl.Cell().Padding(4).Text(v.Reason ?? "—").FontSize(9);
                                        tbl.Cell().Padding(4).Text(v.CodeLocation ?? "—").FontSize(9);
                                    }
                                });
                            }
                            else
                            {
                                col.Item().Text("Нарушений не обнаружено").FontSize(12).Italic();
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

        private async void ExportSummaryPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                var db = App.GetService<AppDbContext>();
                var trainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();
                var allSessions = await db.SessionAnalysis.Where(s => s.IsArchived != true).ToListAsync();
                var projects = await db.Projects.Where(p => p.IsArchived != true).ToListAsync();

                var dialog = new SaveFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    FileName = $"Сводка_стажёров_{DateTime.Now:yyyyMMdd}.pdf"
                };
                if (dialog.ShowDialog() != true) return;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(25);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Inter"));

                        page.Header().Text("Сводный отчёт по стажёрам")
                            .FontSize(18).Bold().AlignCenter();

                        page.Content().Column(col =>
                        {
                            col.Item().Text("");

                            foreach (var t in trainees)
                            {
                                var sessions = allSessions.Where(s => s.TraineeId == t.UserId).OrderBy(s => s.StartTime).ToList();
                                var last = sessions.LastOrDefault();
                                var proj = projects.FirstOrDefault(p => p.TraineeId == t.UserId);
                                double lastPct = (double)(last?.OverallMatchPercent ?? 0);
                                double avgPct = sessions.Any() ? sessions.Average(s => (double)(s.OverallMatchPercent ?? 0)) : 0;

                                col.Item().Text($"Стажёр: {t.LastName} {t.FirstName}".Trim()).FontSize(14).Bold();
                                col.Item().Text($"Проект: {proj?.Title ?? "—"}");
                                col.Item().Text($"Попыток: {sessions.Count}");
                                col.Item().Text($"Средний результат: {(sessions.Any() ? $"{avgPct:F0}%" : "—")}");
                                col.Item().Text($"Последний результат: {(last != null ? $"{lastPct:F0}%" : "—")}");
                                col.Item().Text($"Статус: {(last == null ? "Нет попыток" : lastPct >= 80 ? "Готов" : lastPct >= 50 ? "Доработка" : "Не готов")}");

                                if (sessions.Any())
                                {
                                    col.Item().Text("");
                                    col.Item().Text("История проверок:").FontSize(11).Bold();

                                    col.Item().Table(tbl =>
                                    {
                                        tbl.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(2);
                                            c.ConstantColumn(50);
                                            c.ConstantColumn(50);
                                            c.RelativeColumn(2);
                                        });

                                        tbl.Header(h =>
                                        {
                                            h.Cell().Background("#F1F5F9").Padding(2).Text("Дата").FontSize(9).Bold();
                                            h.Cell().Background("#F1F5F9").Padding(2).Text("% ТЗ").FontSize(9).Bold();
                                            h.Cell().Background("#F1F5F9").Padding(2).Text("% Эталон").FontSize(9).Bold();
                                            h.Cell().Background("#F1F5F9").Padding(2).Text("Статус").FontSize(9).Bold();
                                        });

                                        foreach (var s in sessions)
                                        {
                                            tbl.Cell().Padding(3).Text(s.StartTime?.ToString("dd.MM.yyyy HH:mm") ?? "—").FontSize(9);
                                            tbl.Cell().Padding(3).Text($"{s.OverallMatchPercent:F0}%").FontSize(9);
                                            tbl.Cell().Padding(3).Text($"{s.ReferenceMatchPercent:F0}%").FontSize(9);
                                            tbl.Cell().Padding(3).Text(s.Status == "Completed" ? "Завершена" : "В процессе").FontSize(9);
                                        }
                                    });
                                }

                                col.Item().Text("");
                                col.Item().BorderBottom(1).BorderColor("#E5E7EB");
                                col.Item().Text("");
                            }
                        });
                    });
                }).GeneratePdf(dialog.FileName);

                MessageBox.Show("Сводный отчёт сохранён!", "Готово");
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
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