using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class SystemResourcesPage : UserControl
    {
        public SystemResourcesPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadAllDataAsync();
        }
        private const int SYSTEM_SPEC_ID = 2028;

        private async System.Threading.Tasks.Task LoadAllDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();

                var metrics = await db.QualityMetrics.ToListAsync();
                MetricsGrid.ItemsSource = metrics.Select(m => new MetricRow
                {
                    MetricId = m.MetricId,
                    MetricName = m.MetricName,
                    MetricType = m.MetricType switch
                    {
                        "Roslyn" => "Синтаксис",
                        "NetArchTest" => "Архитектура",
                        "AI" => "Семантическая",
                        _ => m.MetricType
                    },
                    DefaultThreshold = m.DefaultThreshold,
                    IsActive = m.IsActive == true
                }).ToList();

                var markers = await db.RequirementMarkers.Where(m => m.SpecificationId == SYSTEM_SPEC_ID).ToListAsync();
                MarkersGrid.ItemsSource = markers.Select(m => new MarkerRow
                {
                    MarkerId = m.MarkerId,
                    Phrase = m.Phrase,
                    RequirementType = m.RequirementType switch
                    {
                        "Functional" => "Функциональное",
                        "Architectural" => "Архитектурное",
                        "Metric" => "Метрика",
                        _ => m.RequirementType
                    },
                    IsActive = m.IsActive
                }).ToList();

                var prompts = await db.PromptTemplates.ToListAsync();
                System.Diagnostics.Debug.WriteLine($"Prompts count: {prompts.Count}");
                foreach (var p in prompts)
                    System.Diagnostics.Debug.WriteLine($"  Prompt: {p.PromptId}, Type: {p.PromptType}, Active: {p.IsActive}");

                PromptsGrid.ItemsSource = prompts.Select(p => new PromptRow
                {
                    PromptId = p.PromptId,
                    PromptType = p.PromptType switch
                    {
                        "Extraction" => "Извлечение требований",
                        "Judgment" => "Проверка кода",
                        _ => p.PromptType
                    },
                    SystemPrompt = p.SystemPrompt,
                    IsActive = p.IsActive
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"Grid ItemsSource set: {PromptsGrid.ItemsSource != null}, Count: {(PromptsGrid.ItemsSource as IEnumerable<PromptRow>)?.Count()}");

                var tokens = await db.AccessTokens.ToListAsync();
                TokensGrid.ItemsSource = tokens.Select(t => new TokenRow { TokenId = t.TokenId, TokenValue = t.TokenValue, Description = t.Description ?? "", IsActive = t.IsActive == true }).ToList();

            }
            catch { }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelMetrics == null || PanelMarkers == null || PanelTokens == null || PanelPrompts == null)
                return;

            PanelMetrics.Visibility = TabMetrics.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelMarkers.Visibility = TabMarkers.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelTokens.Visibility = TabTokens.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelPrompts.Visibility = TabPrompts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // Метрики
        private async void MetricCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is MetricRow row)
            {
                var db = App.GetService<AppDbContext>();
                var m = await db.QualityMetrics.FindAsync(row.MetricId);
                if (m != null) { m.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }

        private async void AddMetricButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MetricDialog();
            if (dialog.ShowDialog() == true) await LoadAllDataAsync();
        }

        // Маркеры
        private async void MarkerCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is MarkerRow row)
            {
                var db = App.GetService<AppDbContext>();
                var m = await db.RequirementMarkers.FindAsync(row.MarkerId);
                if (m != null) { m.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }
        private async void AddMarkerButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MarkerDialog(SYSTEM_SPEC_ID);
            if (dialog.ShowDialog() == true)
            {
                await LoadAllDataAsync();
                MarkersGrid.Items.Refresh();
            }
        }

        // Токены
        private async void TokenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TokenRow row)
            {
                var db = App.GetService<AppDbContext>();
                var t = await db.AccessTokens.FindAsync(row.TokenId);
                if (t != null) { t.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }
        private async void AddTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TokenDialog();
            if (dialog.ShowDialog() == true) await LoadAllDataAsync();
        }

        // Промпты
        private async void PromptCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is PromptRow row)
            {
                var db = App.GetService<AppDbContext>();
                var p = await db.PromptTemplates.FindAsync(row.PromptId);
                if (p != null) { p.IsActive = cb.IsChecked == true; await db.SaveChangesAsync(); }
            }
        }
        private async void AddPromptButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PromptDialog(SYSTEM_SPEC_ID);
            if (dialog.ShowDialog() == true)
            {
                await LoadAllDataAsync();
                PromptsGrid.Items.Refresh();
            }
        }
    }
    public class MetricRow
    {
        public int MetricId { get; set; }
        public string MetricName { get; set; } = "";
        public string MetricType { get; set; } = "";
        public decimal? DefaultThreshold { get; set; }
        public bool IsActive { get; set; }
    }

    public class MarkerRow
    {
        public int MarkerId { get; set; }
        public string Phrase { get; set; } = "";
        public string RequirementType { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class TokenRow
    {
        public int TokenId { get; set; }
        public string TokenValue { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class PromptRow
    {
        public int PromptId { get; set; }
        public string PromptType { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public bool IsActive { get; set; }
    }
}