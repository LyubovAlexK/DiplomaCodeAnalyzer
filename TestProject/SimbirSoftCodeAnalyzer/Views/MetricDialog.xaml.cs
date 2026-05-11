using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class MetricDialog : Window
    {
        public MetricDialog()
        {
            InitializeComponent();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string type = ((ComboBoxItem)TypeCombo.SelectedItem).Tag.ToString();
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("Введите название"); return; }

            var db = App.GetService<AppDbContext>();

            // Проверка на дубликат
            var existing = await db.QualityMetrics.FirstOrDefaultAsync(m => m.MetricName == name);
            if (existing != null)
            {
                MessageBox.Show("Метрика с таким названием уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? threshold = null;
            if (decimal.TryParse(ThresholdBox.Text.Trim(), out decimal t)) threshold = t;

            db.QualityMetrics.Add(new QualityMetric { MetricName = name, MetricType = type, DefaultThreshold = threshold, IsActive = true });
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
    }
}