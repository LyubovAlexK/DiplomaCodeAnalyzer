using System;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class UsersPage : UserControl
    {
        public UsersPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadUsers();
        }

        private async void LoadUsers()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var users = await db.Users
                    .Where(u => u.IsActive == true)
                    .OrderBy(u => u.RoleId)
                    .ToListAsync();

                UsersGrid.ItemsSource = users;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }
    }
}