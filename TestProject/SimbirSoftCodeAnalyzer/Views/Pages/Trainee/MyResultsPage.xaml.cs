using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class MyResultsPage : UserControl
    {
        public MyResultsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadResults();
        }

        private async void LoadResults()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var traineeId = App.CurrentUser?.UserId ?? 0;

                var verdicts = await db.AuditVerdicts
                    .Where(v => db.SessionAnalysis
                        .Where(s => s.TraineeId == traineeId)
                        .Select(s => s.SessionId)
                        .Contains(v.SessionId))
                    .OrderByDescending(v => v.CreatedAt)
                    .Take(100)
                    .ToListAsync();

                ResultsGrid.ItemsSource = verdicts;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadResults();
        }
    }
}