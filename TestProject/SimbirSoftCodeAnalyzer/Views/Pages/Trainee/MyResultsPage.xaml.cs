using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class MyResultsPage : UserControl
    {
        private List<VerdictRow> _allVerdicts = new();
        private bool _isLoaded;

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
                ProjectFilterCombo.Items.Clear();
                foreach (var p in projects)
                    ProjectFilterCombo.Items.Add(new ComboBoxItem { Content = p.Title, Tag = p.ProjectId });

                if (_forcedProjectId.HasValue)
                {
                    for (int i = 0; i < ProjectFilterCombo.Items.Count; i++)
                    {
                        if (ProjectFilterCombo.Items[i] is ComboBoxItem item && item.Tag is int id && id == _forcedProjectId.Value)
                        {
                            _isLoaded = true;
                            ProjectFilterCombo.SelectedIndex = i;
                            _ = LoadResultsAsync(id);
                            _forcedProjectId = null;
                            return;
                        }
                    }
                }

                if (ProjectFilterCombo.Items.Count > 0)
                {
                    ProjectFilterCombo.SelectedIndex = 0;
                    _isLoaded = true;
                }
            }
            catch { }
        }

        private async void ProjectFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ProjectFilterCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
                await LoadResultsAsync(projectId);
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

                if (lastSession == null)
                {
                    PlaceholderText.Text = "Нет завершённых проверок для этого проекта";
                    PlaceholderText.Visibility = Visibility.Visible;
                    HideAllPanels();
                    return;
                }

                PlaceholderText.Visibility = Visibility.Collapsed;
                ShowAllPanels();

                var verdicts = await db.AuditVerdicts
                    .Where(v => v.SessionId == lastSession.SessionId)
                    .ToListAsync();

                double pct = (double)(lastSession.OverallMatchPercent ?? 0);
                int roslynFail = verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                int archFail = verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                int aiFail = verdicts.Count(v => v.AiModel == "GigaChat" && !v.IsPassed);

                MatchPercentText.Text = $"{pct:F0}%";
                RoslynFailText.Text = roslynFail.ToString();
                ArchFailText.Text = archFail.ToString();
                AiFailText.Text = aiFail.ToString();

                DrawGaugeChart(pct);
                GaugePercentText.Text = $"{pct:F0}%";

                _allVerdicts = verdicts.Select(v => new VerdictRow
                {
                    AiModel = v.AiModel switch
                    {
                        "Roslyn" => "Синтаксис",
                        "NetArchTest" => "Архитектура",
                        "GigaChat" => "AI Judge",
                        "GraphAnalysis" => "Граф",
                        _ => v.AiModel ?? "—"
                    },
                    Reason = v.Reason ?? "—",
                    CodeLocation = v.CodeLocation ?? "—",
                    PassedText = v.IsPassed ? "Пройден" : "Нарушение",
                    PassedBg = v.IsPassed ? "#D1FAE5" : "#FEE2E2",
                    PassedFg = v.IsPassed ? "#10B981" : "#EF4444",
                    CreatedAt = v.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    IsPassed = v.IsPassed,
                    IsSelected = false
                }).ToList();

                ApplyVerdictFilter();

                var reference = await db.ReferenceProjects
                    .FirstOrDefaultAsync(r => r.SpecificationId == lastSession.SpecificationId);
                if (reference != null)
                {
                    var roslynResult = new CodeAnalysisResult
                    {
                        TotalMethods = roslynFail + verdicts.Count(v => v.AiModel == "Roslyn" && v.IsPassed),
                        MethodsExceedingComplexity = roslynFail
                    };
                    var refService = App.GetService<ReferenceService>();
                    var refResult = await refService.CompareWithReference(reference.ReferenceId, roslynResult);
                    ReferenceText.Text = reference != null
                        ? $"Методов с нарушениями: {roslynResult.MethodsExceedingComplexity} / {reference.TotalMethods}\n" +
                          $"Расхождений с эталоном: {refResult.Count}"
                        : "Эталон не задан";
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

        private void DrawGaugeChart(double percent)
        {
            GaugeCanvas.Children.Clear();
            double cx = 60, cy = 80, r = 50, thickness = 12;

            for (double a = 0; a <= 180; a += 0.3)
            {
                double rad = (180 + a) * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy - (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy - r * Math.Sin(rad),
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F6")),
                    StrokeThickness = 2
                });
            }

            string colorHex = percent >= 80 ? "#10B981" : percent >= 50 ? "#F59E0B" : "#EF4444";
            double sweep = percent / 100.0 * 180;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            for (double a = 0; a <= sweep; a += 0.3)
            {
                double rad = (180 + a) * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy - (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy - r * Math.Sin(rad),
                    Stroke = brush,
                    StrokeThickness = 2
                });
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
            if (VerdictFilterCombo?.SelectedItem is ComboBoxItem filterItem)
            {
                string tag = filterItem.Tag?.ToString() ?? "all";
                if (tag == "failed") filtered = filtered.Where(v => !v.IsPassed);
                else if (tag == "passed") filtered = filtered.Where(v => v.IsPassed);
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
            KpiGrid.Visibility = Visibility.Collapsed;
            GaugeTableGrid.Visibility = Visibility.Collapsed;
            ReferencePanel.Visibility = Visibility.Collapsed;
            BottomPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowAllPanels()
        {
            KpiGrid.Visibility = Visibility.Visible;
            GaugeTableGrid.Visibility = Visibility.Visible;
            ReferencePanel.Visibility = Visibility.Visible;
            BottomPanel.Visibility = Visibility.Visible;
        }

        private void VerdictsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (VerdictsGrid.SelectedItem is VerdictRow v)
                MessageBox.Show($"Тип: {v.AiModel}\nОписание: {v.Reason}\nФайл: {v.CodeLocation}",
                    "Детали вердикта", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SendToMentor_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Результат отправлен наставнику", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Экспорт в PDF будет доступен позже", "Заглушка", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class VerdictRow
    {
        public string AiModel { get; set; } = "";
        public string Reason { get; set; } = "";
        public string CodeLocation { get; set; } = "";
        public string PassedText { get; set; } = "";
        public string PassedBg { get; set; } = "";
        public string PassedFg { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public bool IsPassed { get; set; }
        public bool IsSelected { get; set; }
    }
}