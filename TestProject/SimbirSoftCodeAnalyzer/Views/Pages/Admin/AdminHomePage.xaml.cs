using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class AdminHomePage : UserControl
    {
        public AdminHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();

                var usersCount = await db.Users.CountAsync(u => u.IsActive == true);
                var specsCount = await db.Specifications.CountAsync(s => s.IsActive == true);
                var projectsCount = await db.Projects.CountAsync(p => p.IsArchived != true);
                var sessionsCount = await db.SessionAnalysis.CountAsync();

                UsersCountText.Text = $"Пользователей: {usersCount}";
                SpecsCountText.Text = $"ТЗ: {specsCount}";
                ProjectsCountText.Text = $"Проектов: {projectsCount}";
                SessionsCountText.Text = $"Сессий: {sessionsCount}";

                // Последние действия
                RecentActionsPanel.Children.Clear();

                // Последние ТЗ
                var recentSpecs = await db.Specifications
                    .Where(s => s.IsActive == true)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                var userIds = recentSpecs.Select(s => s.CreatedBy).Distinct().ToList();
                var users = await db.Users.Where(u => userIds.Contains(u.UserId)).ToListAsync();

                foreach (var s in recentSpecs)
                {
                    var user = users.FirstOrDefault(u => u.UserId == s.CreatedBy);
                    string userName = user != null ? $"{user.LastName} {user.FirstName}" : "Система";
                    AddActionItem($"{s.CreatedAt:dd.MM.yyyy HH:mm} — {userName} загрузил ТЗ «{s.Title}»");
                }

                // Последние пользователи
                var recentUsers = await db.Users
                    .Where(u => u.IsActive == true)
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                foreach (var u in recentUsers)
                    AddActionItem($"{u.CreatedAt:dd.MM.yyyy HH:mm} — Создан пользователь «{u.Login}» ({u.LastName} {u.FirstName})");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void AddActionItem(string text)
        {
            RecentActionsPanel.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("H4Text"),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }
}