using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimbirSoftCodeAnalyzer.Views;
using AI.Extractors;
using AI.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class SpecificationsPage : UserControl
    {
        private string? _selectedFilePath;
        private int _selectedSpecId;
        private async Task<string> GetActiveGigaChatKeyAsync()
        {
            var db = App.GetService<AppDbContext>();
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

        public SpecificationsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadSpecsAsync();
        }

        private async System.Threading.Tasks.Task LoadSpecsAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var specs = await db.Specifications.Where(s => s.IsActive == true).OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        s.SpecificationId, s.Title,
                        ТипАнализа = s.ExtractionType == "GigaChat" ? "ИИ" : "Локальный",
                        Дата = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("dd.MM.yyyy") : "—",
                        Требований = db.Requirements.Count(r => r.SpecificationId == s.SpecificationId)
                    }).ToListAsync();
                SpecsGrid.ItemsSource = specs;
            }
            catch { }
        }

        private void BrowseTzButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл технического задания",
                Filter = "Документы|*.pdf;*.docx;*.txt;*.odt|Все файлы|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                TzPathTextBox.Text = _selectedFilePath;
            }
        }

        private async void AnalyzeTzButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) { MessageBox.Show("Выберите файл ТЗ"); return; }

            // Показать прогресс
            AnalysisProgressBorder.Visibility = Visibility.Visible;
            AnalysisStatusText.Text = "Анализ текста...";
            AnalyzeTzButton.IsEnabled = false;

            try
            {
                var db = App.GetService<AppDbContext>();
                var dictService = App.GetService<DictionaryService>();
                var specService = App.GetService<SpecificationService>();
                bool isOnline = AiRadio.IsChecked == true;

                AnalysisStatusText.Text = "Сохранение ТЗ...";
                var spec = new ProjectSpecification
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(_selectedFilePath),
                    FilePath = _selectedFilePath,
                    ExtractionType = isOnline ? "GigaChat" : "Local",
                    CreatedBy = App.CurrentUser?.UserId ?? 1,
                    IsActive = true
                };
                int specId = await specService.SaveAsync(spec);

                AnalysisStatusText.Text = isOnline ? "Отправка запроса в GigaChat..." : "Локальный анализ...";

                string authKey = null;
                if (isOnline)
                {
                    var token = await db.AccessTokens
                        .FirstOrDefaultAsync(t => t.Description == "GigaChat" && t.IsActive == true);

                    if (token == null)
                    {
                        MessageBox.Show("Не найден активный токен GigaChat. Добавьте токен в разделе «Системные ресурсы» (Администратор).",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    authKey = token.TokenValue;
                }

                var factory = new ExtractorFactory(dictService, authKey);
                var extractor = factory.Create(isOnline);
                var result = await extractor.ExtractAsync(_selectedFilePath, specId);

                AnalysisStatusText.Text = "Сохранение требований...";
                if (result.Requirements.Any())
                {
                    foreach (var req in result.Requirements)
                    {
                        req.SpecificationId = specId;
                        req.CreatedAt = DateTime.Now;
                        req.IsActive = true;
                    }
                    db.Requirements.AddRange(result.Requirements);
                    await db.SaveChangesAsync();
                }

                MessageBox.Show($"Анализ завершён. Найдено требований: {result.Requirements.Count}", "Готово");
                await LoadSpecsAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
            finally
            {
                AnalysisProgressBorder.Visibility = Visibility.Collapsed;
                AnalyzeTzButton.IsEnabled = true;
            }
        }

        private async void SpecsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpecsGrid.SelectedItem == null) { RequirementsPanel.Visibility = Visibility.Collapsed; return; }
            dynamic row = SpecsGrid.SelectedItem;
            _selectedSpecId = row.SpecificationId;
            RequirementsTitle.Text = $"Требования для: {row.Title}";
            await LoadRequirementsAsync(_selectedSpecId);
            RequirementsPanel.Visibility = Visibility.Visible;
        }

        private async System.Threading.Tasks.Task LoadRequirementsAsync(int specId)
        {
            var db = App.GetService<AppDbContext>();
            var reqs = await db.Requirements
                .Where(r => r.SpecificationId == specId && r.IsActive != false)
                .ToListAsync();

            var rows = new List<RequirementRow>();
            foreach (var r in reqs)
            {
                rows.Add(new RequirementRow
                {
                    RequirementId = r.RequirementId,
                    IsActive = r.IsActive == true,
                    Тип = r.RequirementType switch { "Functional" => "Функц.", "Architectural" => "Архит.", "Metric" => "Метрика", _ => r.RequirementType ?? "—" },
                    Title = r.Title,
                    Важность = r.Severity switch { "Fatal" => "Критично", "Major" => "Важно", "Minor" => "Средне", _ => r.Severity ?? "—" },
                    SeverityBg = r.Severity switch { "Fatal" => "#FEE2E2", "Major" => "#FEF3C7", "Minor" => "#D1FAE5", _ => "#F3F4F6" },
                    SeverityFg = r.Severity switch { "Fatal" => "#EF4444", "Major" => "#D97706", "Minor" => "#10B981", _ => "#6B7280" }
                });
            }
            RequirementsGrid.ItemsSource = rows;
        }

        private async void RequirementCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is RequirementRow row)
            {
                var db = App.GetService<AppDbContext>();
                var entity = await db.Requirements.FindAsync(row.RequirementId);
                if (entity != null) { entity.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }

        private async void AddRequirement_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RequirementDialog(_selectedSpecId);
            if (dialog.ShowDialog() == true) await LoadRequirementsAsync(_selectedSpecId);
        }

        private async void EditRequirement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                dynamic row = btn.DataContext;
                int reqId = row.RequirementId;
                var db = App.GetService<AppDbContext>();
                var req = await db.Requirements.FindAsync(reqId);
                if (req != null)
                {
                    var dialog = new RequirementDialog(_selectedSpecId, req);
                    if (dialog.ShowDialog() == true) await LoadRequirementsAsync(_selectedSpecId);
                }
            }
        }

        private void RequirementsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditRequirement_Click(sender, e);
        }

        private async void MarkersButton_Click(object sender, RoutedEventArgs e)
        {
            var markerDialog = new MarkerDialog(_selectedSpecId, 0, "", "Functional", "Major");
            if (markerDialog.ShowDialog() == true) MessageBox.Show("Маркер сохранён", "Готово");
        }

        private async void PromptsButton_Click(object sender, RoutedEventArgs e)
        {
            var promptDialog = new PromptDialog(_selectedSpecId, 0, "Extraction", "", "");
            if (promptDialog.ShowDialog() == true) MessageBox.Show("Промпт сохранён", "Готово");
        }
        private async void DeleteRequirement_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteRequirement: selected row = {RequirementsGrid.SelectedItem != null}, _selectedSpecId = {_selectedSpecId}");

            if (RequirementsGrid.SelectedItem is not RequirementRow row)
            {
                MessageBox.Show("Выберите требование для удаления", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"DeleteRequirement: reqId = {row.RequirementId}, title = {row.Title}");

            if (MessageBox.Show($"Удалить требование «{row.Title}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var db = App.GetService<AppDbContext>();
                    var entity = await db.Requirements.FindAsync(row.RequirementId);
                    if (entity != null)
                    {
                        entity.IsActive = false;
                        await db.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine("DeleteRequirement: saved successfully");
                        await LoadRequirementsAsync(_selectedSpecId);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("DeleteRequirement: entity not found");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteRequirement error: {ex.Message}");
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
            HelpPopup.PlacementTarget = HelpButton;
        }
    }
    public class RequirementRow
    {
        public int RequirementId { get; set; }
        public bool IsActive { get; set; }
        public string Тип { get; set; } = "";
        public string Title { get; set; } = "";
        public string Важность { get; set; } = "";
        public string SeverityBg { get; set; } = "";
        public string SeverityFg { get; set; } = "";
    }
}