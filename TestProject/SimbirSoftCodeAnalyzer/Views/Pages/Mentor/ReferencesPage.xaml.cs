using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class ReferencesPage : UserControl
    {
        private string? _refPath;
        private int _selectedRefId;
        private List<RuleRow> _ruleRows = new();

        public ReferencesPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();

                var specs = await db.Specifications.Where(s => s.IsActive == true).ToListAsync();
                SpecCombo.Items.Clear();
                foreach (var s in specs)
                    SpecCombo.Items.Add(new ComboBoxItem { Content = s.Title, Tag = s.SpecificationId });
                if (SpecCombo.Items.Count > 0) SpecCombo.SelectedIndex = 0;

                await LoadReferencesAsync();
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadReferencesAsync()
        {
            var db = App.GetService<AppDbContext>();
            var refs = await db.ReferenceProjects.Where(r => r.IsActive).OrderByDescending(r => r.AnalyzedAt).ToListAsync();
            var specIds = refs.Select(r => r.SpecificationId).Distinct();
            var specTitles = await db.Specifications.Where(s => specIds.Contains(s.SpecificationId)).ToDictionaryAsync(s => s.SpecificationId, s => s.Title);

            ReferencesGrid.ItemsSource = refs.Select(r => new
            {
                r.ReferenceId,
                SpecTitle = specTitles.GetValueOrDefault(r.SpecificationId, "—"),
                r.ProjectPath,
                TotalMethods = r.TotalMethods.HasValue ? r.TotalMethods.Value.ToString() : "—",
                AvgComplexity = r.AvgCyclomaticComplexity.HasValue ? r.AvgCyclomaticComplexity.Value.ToString("F1") : "—",
                Date = r.AnalyzedAt.ToString("dd.MM.yyyy")
            }).ToList();
        }

        private void BrowseRefButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Выберите папку с эталонным проектом" };
            if (dialog.ShowDialog() == true)
            {
                _refPath = dialog.FolderName;
                RefPathTextBox.Text = _refPath;
            }
        }

        private async void AnalyzeRefButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_refPath) || !Directory.Exists(_refPath))
            { MessageBox.Show("Выберите папку с эталонным проектом"); return; }
            if (SpecCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int specId)
            { MessageBox.Show("Выберите ТЗ"); return; }

            try
            {
                var roslynAnalyzer = App.GetService<RoslynSyntaxAnalyzer>();
                var referenceService = App.GetService<ReferenceService>();
                var result = await roslynAnalyzer.AnalyzeProjectAsync(_refPath);
                await referenceService.SaveReferenceAsync(specId, _refPath, result);
                MessageBox.Show($"Эталон сохранён. Методов: {result.TotalMethods}", "Готово");
                await LoadReferencesAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
        }

        private async void ReferencesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ReferencesGrid.SelectedItem == null) return;
            dynamic row = ReferencesGrid.SelectedItem;
            _selectedRefId = row.ReferenceId;
            RulesTitle.Text = $"Правила сравнения для: {row.SpecTitle}";

            var db = App.GetService<AppDbContext>();
            var rules = await db.ReferenceRules.Where(r => r.ReferenceId == _selectedRefId).ToListAsync();

            _ruleRows = rules.Select(r => new RuleRow
            {
                RuleId = r.RuleId,
                Метрика = r.MetricName switch
                {
                    "TotalMethods" => "Количество методов",
                    "AvgCyclomaticComplexity" => "Средняя сложность",
                    "AvgExecutableLines" => "Среднее строк",
                    "MethodsExceedingComplexity" => "Сложных методов",
                    _ => r.MetricName
                },
                Тип = r.ThresholdType switch { "Range" => "Диапазон", "Max" => "Максимум", _ => r.ThresholdType },
                ThresholdValue = r.ThresholdValue ?? 0,
                IsEnabled = r.IsEnabled
            }).ToList();

            RulesGrid.ItemsSource = _ruleRows;
            RulesPanel.Visibility = Visibility.Visible;
        }

        private async void RuleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is RuleRow row)
            {
                var db = App.GetService<AppDbContext>();
                var rule = await db.ReferenceRules.FindAsync(row.RuleId);
                if (rule != null) { rule.IsEnabled = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }

        private async void RulesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is RuleRow row && e.Column.Header.ToString() == "Порог")
            {
                if (e.EditingElement is TextBox tb && decimal.TryParse(tb.Text, out decimal val))
                {
                    var db = App.GetService<AppDbContext>();
                    var rule = await db.ReferenceRules.FindAsync(row.RuleId);
                    if (rule != null) { rule.ThresholdValue = val; await db.SaveChangesAsync(); }
                }
            }
        }

        private async void SaveRulesButton_Click(object sender, RoutedEventArgs e)
        {
            var db = App.GetService<AppDbContext>();
            foreach (var row in _ruleRows)
            {
                var rule = await db.ReferenceRules.FindAsync(row.RuleId);
                if (rule != null) { rule.ThresholdValue = row.ThresholdValue; rule.IsEnabled = row.IsEnabled; }
            }
            await db.SaveChangesAsync();
            MessageBox.Show("Изменения сохранены", "Готово");
        }

        private async void DeleteRefButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReferencesGrid.SelectedItem == null) { MessageBox.Show("Выберите эталон"); return; }
            dynamic row = ReferencesGrid.SelectedItem;
            if (MessageBox.Show($"Удалить эталон «{row.SpecTitle}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var db = App.GetService<AppDbContext>();
                var entity = await db.ReferenceProjects.FindAsync((int)row.ReferenceId);
                if (entity != null) { entity.IsActive = false; await db.SaveChangesAsync(); await LoadReferencesAsync(); }
            }
        }
    }

    public class RuleRow
    {
        public int RuleId { get; set; }
        public string Метрика { get; set; } = "";
        public string Тип { get; set; } = "";
        public decimal ThresholdValue { get; set; }
        public bool IsEnabled { get; set; }
    }
}