using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class ProjectsPage : UserControl
    {
        public ProjectsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();

                var trainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();
                TraineeCombo.Items.Clear();
                foreach (var t in trainees)
                    TraineeCombo.Items.Add(new ComboBoxItem { Content = $"{t.LastName} {t.FirstName}".Trim(), Tag = t.UserId });
                if (TraineeCombo.Items.Count > 0) TraineeCombo.SelectedIndex = 0;

                var specs = await db.Specifications.Where(s => s.IsActive == true).ToListAsync();
                SpecCombo.Items.Clear();
                foreach (var s in specs)
                    SpecCombo.Items.Add(new ComboBoxItem { Content = s.Title, Tag = s.SpecificationId });
                if (SpecCombo.Items.Count > 0) SpecCombo.SelectedIndex = 0;

                await LoadProjectsAsync();
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            var db = App.GetService<AppDbContext>();
            var projects = await db.Projects.Where(p => p.IsArchived != true).OrderByDescending(p => p.CreatedAt).ToListAsync();
            var userIds = projects.Select(p => p.TraineeId).Distinct().ToList();
            var specIds = projects.Select(p => p.SpecificationId).Distinct().ToList();
            var users = await db.Users.Where(u => userIds.Contains(u.UserId)).ToListAsync();
            var specs = await db.Specifications.Where(s => specIds.Contains(s.SpecificationId)).ToListAsync();

            // Добавляем стажёров без проектов
            var allTrainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();
            var traineesWithoutProject = allTrainees.Where(t => !projects.Any(p => p.TraineeId == t.UserId)).ToList();

            var projectList = projects.Select(p =>
            {
                var trainee = users.FirstOrDefault(u => u.UserId == p.TraineeId);
                var spec = specs.FirstOrDefault(s => s.SpecificationId == p.SpecificationId);
                return new
                {
                    p.ProjectId,
                    p.Title,
                    TraineeName = trainee != null ? $"{trainee.LastName} {trainee.FirstName}".Trim() : "—",
                    SpecTitle = spec?.Title ?? "—",
                    StatusText = p.IsCompleted == true ? "Сдан" : "В работе",
                    StatusBg = p.IsCompleted == true ? "#D1FAE5" : "#FEF3C7",
                    StatusFg = p.IsCompleted == true ? "#10B981" : "#D97706"
                };
            }).ToList();

            // Добавляем строки-заглушки для стажёров без проектов
            foreach (var t in traineesWithoutProject)
            {
                projectList.Add(new
                {
                    ProjectId = 0,
                    Title = "Нет проекта",
                    TraineeName = $"{t.LastName} {t.FirstName}".Trim(),
                    SpecTitle = "—",
                    StatusText = "Не назначен",
                    StatusBg = "#FEE2E2",
                    StatusFg = "#EF4444"
                });
            }

            ProjectsGrid.ItemsSource = projectList;
        }

        private async void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            string title = ProjectTitleBox.Text.Trim();
            string desc = ProjectDescBox.Text.Trim();

            if (string.IsNullOrEmpty(title))
            { MessageBox.Show("Введите название проекта"); return; }
            if (TraineeCombo.SelectedItem is not ComboBoxItem traineeItem || traineeItem.Tag is not int traineeId)
            { MessageBox.Show("Выберите стажёра"); return; }
            if (SpecCombo.SelectedItem is not ComboBoxItem specItem || specItem.Tag is not int specId)
            { MessageBox.Show("Выберите ТЗ"); return; }

            var db = App.GetService<AppDbContext>();
            db.Projects.Add(new Project
            {
                Title = title,
                Description = desc,
                TraineeId = traineeId,
                SpecificationId = specId,
                CreatedBy = App.CurrentUser?.UserId ?? 1,
                IsCompleted = false,
                IsArchived = false
            });
            await db.SaveChangesAsync();

            ProjectTitleBox.Text = "";
            ProjectDescBox.Text = "";
            await LoadProjectsAsync();
            MessageBox.Show("Проект добавлен", "Готово");
        }

        private async void ArchiveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                dynamic row = btn.DataContext;
                if (MessageBox.Show($"Архивировать проект «{row.Title}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var db = App.GetService<AppDbContext>();
                    var project = await db.Projects.FindAsync((int)row.ProjectId);
                    if (project != null) { project.IsArchived = true; await db.SaveChangesAsync(); await LoadProjectsAsync(); }
                }
            }
        }
    }
}