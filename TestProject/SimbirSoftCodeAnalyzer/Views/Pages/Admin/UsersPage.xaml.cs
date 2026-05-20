using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class UsersPage : UserControl
    {
        public UsersPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var users = await db.Users.OrderBy(u => u.RoleId).ToListAsync();
                var roles = await db.Roles.ToDictionaryAsync(r => r.RoleId, r => r.RoleName);
                var mentors = await db.Users.Where(u => u.RoleId == 2 && u.IsActive == true).ToListAsync();
                var projects = await db.Projects.Where(p => p.IsArchived != true).ToListAsync();

                UsersGrid.ItemsSource = users.Select(u =>
                {
                    bool hasMentor = true;
                    if (u.RoleId == 3)
                    {
                        var project = projects.FirstOrDefault(p => p.TraineeId == u.UserId);
                        if (project != null)
                        {
                            var mentor = mentors.FirstOrDefault(m => m.UserId == project.CreatedBy);
                            hasMentor = mentor != null;
                        }
                        else
                        {
                            hasMentor = false;
                        }
                    }

                    return new
                    {
                        u.UserId,
                        u.Login,
                        FullName = $"{u.LastName} {u.FirstName} {u.MiddleName}".Trim(),
                        RoleName = roles.GetValueOrDefault(u.RoleId, "—") switch
                        {
                            "Admin" => "Администратор",
                            "Mentor" => "Наставник",
                            "Trainee" => "Стажёр",
                            _ => roles.GetValueOrDefault(u.RoleId, "—")
                        },
                        StatusText = u.IsActive == true
                            ? (u.RoleId == 3 && !hasMentor ? "⚠️ Нет наставника" : "Активен")
                            : "Неактивен",
                        StatusBg = u.IsActive == true
                            ? (u.RoleId == 3 && !hasMentor ? "#FEF3C7" : "#D1FAE5")
                            : "#FEE2E2",
                        StatusFg = u.IsActive == true
                            ? (u.RoleId == 3 && !hasMentor ? "#D97706" : "#10B981")
                            : "#EF4444"
                    };
                }).ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private async void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UserDialog();
            if (dialog.ShowDialog() == true) await LoadUsersAsync();
        }

        private async void EditUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                dynamic row = btn.DataContext;
                int userId = row.UserId;
                var db = App.GetService<AppDbContext>();
                var user = await db.Users.FindAsync(userId);
                if (user != null)
                {
                    var dialog = new UserDialog(user);
                    if (dialog.ShowDialog() == true) await LoadUsersAsync();
                }
            }
        }

        private async void DeletePermanentlyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                dynamic row = btn.DataContext;
                int userId = (int)row.UserId;
                string login = row.Login;

                if (userId == (App.CurrentUser?.UserId ?? -1))
                {
                    MessageBox.Show("Нельзя удалить самого себя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var db = App.GetService<AppDbContext>();
                var user = await db.Users.FindAsync(userId);
                if (user == null) return;

                // Нельзя удалить последнего админа
                if (user.RoleId == 1)
                {
                    var adminCount = await db.Users.CountAsync(u => u.RoleId == 1);
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("Нельзя удалить последнего администратора", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (MessageBox.Show($"Удалить пользователя «{login}» безвозвратно?\n\nВсе связанные данные (проекты, сессии, вердикты) будут также удалены.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    // Удаляем связанные данные
                    var projects = await db.Projects.Where(p => p.TraineeId == userId || p.CreatedBy == userId).ToListAsync();
                    foreach (var project in projects)
                    {
                        var sessions = await db.SessionAnalysis.Where(s => s.ProjectId == project.ProjectId).ToListAsync();
                        foreach (var session in sessions)
                        {
                            var verdicts = await db.AuditVerdicts.Where(v => v.SessionId == session.SessionId).ToListAsync();
                            db.AuditVerdicts.RemoveRange(verdicts);
                        }
                        db.SessionAnalysis.RemoveRange(sessions);
                    }
                    db.Projects.RemoveRange(projects);

                    // Удаляем пользователя
                    db.Users.Remove(user);
                    await db.SaveChangesAsync();
                    await LoadUsersAsync();
                    MessageBox.Show($"Пользователь «{login}» удалён", "Готово");
                }
            }
        }
        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                dynamic row = btn.DataContext;
                int userId = (int)row.UserId;
                string login = row.Login;
                string roleName = row.RoleName;

                if (userId == (App.CurrentUser?.UserId ?? -1))
                {
                    MessageBox.Show("Нельзя удалить самого себя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var db = App.GetService<AppDbContext>(); // ← ОДИН раз здесь

                if (roleName == "Администратор" || roleName == "Админ")
                {
                    var adminCount = await db.Users.CountAsync(u => u.IsActive == true && u.RoleId == 1);
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("Нельзя удалить последнего администратора системы", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (MessageBox.Show($"Деактивировать пользователя «{login}»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var user = await db.Users.FindAsync(userId);
                    if (user != null) { user.IsActive = false; await db.SaveChangesAsync(); await LoadUsersAsync(); }
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadUsersAsync();
    }
}