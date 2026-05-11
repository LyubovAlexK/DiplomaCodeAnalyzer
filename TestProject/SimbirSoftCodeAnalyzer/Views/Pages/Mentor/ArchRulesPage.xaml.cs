using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class ArchRulesPage : UserControl
    {
        public ArchRulesPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadRulesAsync();
        }

        private async System.Threading.Tasks.Task LoadRulesAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var rules = await db.ArchRules
                    .Where(r => r.IsActive == true)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var rows = rules.Select(r =>
                {
                    var parts = (r.RuleJson ?? "namespace:;forbidden:").Split(';');
                    return new ArchRuleRow
                    {
                        RuleId = r.RuleId,
                        RuleName = r.RuleName,
                        SourceNs = parts.Length > 0 ? parts[0].Replace("namespace:", "").Trim() : "",
                        ForbiddenNs = parts.Length > 1 ? parts[1].Replace("forbidden:", "").Trim() : "",
                        IsActive = r.IsActive == true
                    };
                }).ToList();

                RulesGrid.ItemsSource = rows;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private async void AddRuleButton_Click(object sender, RoutedEventArgs e)
        {
            string name = RuleNameBox.Text.Trim();
            string source = SourceNsBox.Text.Trim();
            string forbidden = ForbiddenNsBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(forbidden))
            { MessageBox.Show("Заполните все поля"); return; }

            var db = App.GetService<AppDbContext>();
            db.ArchRules.Add(new ArchRule
            {
                ProjectId = 1,
                RuleName = name,
                RuleJson = $"namespace:{source};forbidden:{forbidden}",
                CreatedBy = App.CurrentUser?.UserId ?? 1,
                IsActive = true
            });
            await db.SaveChangesAsync();

            RuleNameBox.Text = "";
            SourceNsBox.Text = "";
            ForbiddenNsBox.Text = "";
            await LoadRulesAsync();
            MessageBox.Show("Правило добавлено", "Готово");
        }

        private async void RuleActiveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ArchRuleRow row)
            {
                var db = App.GetService<AppDbContext>();
                var rule = await db.ArchRules.FindAsync(row.RuleId);
                if (rule != null) { rule.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }

        private async void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is not ArchRuleRow row)
            { MessageBox.Show("Выберите правило для удаления"); return; }

            if (MessageBox.Show($"Удалить правило «{row.RuleName}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var db = App.GetService<AppDbContext>();
                var rule = await db.ArchRules.FindAsync(row.RuleId);
                if (rule != null) { rule.IsActive = false; await db.SaveChangesAsync(); await LoadRulesAsync(); }
            }
        }
        public class ArchRuleRow
        {
            public int RuleId { get; set; }
            public string RuleName { get; set; } = "";
            public string SourceNs { get; set; } = "";
            public string ForbiddenNs { get; set; } = "";
            public bool IsActive { get; set; }
        }
    }
}