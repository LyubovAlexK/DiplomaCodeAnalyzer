using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class UserDialog : Window
    {
        private readonly int _userId;

        public UserDialog(User? user = null)
        {
            InitializeComponent();
            LoadRoles();

            if (user != null)
            {
                _userId = user.UserId;
                LoginBox.Text = user.Login;
                LastNameBox.Text = user.LastName ?? "";
                FirstNameBox.Text = user.FirstName ?? "";
                MiddleNameBox.Text = user.MiddleName ?? "";
                PasswordBox.Text = "";

                // Скрываем выбор роли для администратора
                if (user.RoleId == 1)
                {
                    RoleCombo.Visibility = Visibility.Collapsed;
                    RoleLabel.Visibility = Visibility.Collapsed;
                }

                // Блокируем смену роли, если это текущий пользователь
                if (user.UserId == App.CurrentUser?.UserId)
                {
                    RoleCombo.IsEnabled = false;
                }

                // Статус
                foreach (ComboBoxItem item in StatusCombo.Items)
                    if (item.Tag.ToString() == (user.IsActive == true ? "True" : "False"))
                        item.IsSelected = true;

                // Выбираем роль
                foreach (ComboBoxItem item in RoleCombo.Items)
                    if (item.Tag is int roleId && roleId == user.RoleId)
                        item.IsSelected = true;

                Title = "Редактирование пользователя";
            }
        }

        private async void LoadRoles()
        {
            var db = App.GetService<AppDbContext>();
            var roles = await db.Roles.ToListAsync();
            foreach (var r in roles)
            {
                string displayName = r.RoleName switch
                {
                    "Admin" => "Администратор",
                    "Mentor" => "Наставник",
                    "Trainee" => "Стажёр",
                    _ => r.RoleName
                };
                RoleCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = r.RoleId });
            }
            if (RoleCombo.Items.Count > 0) RoleCombo.SelectedIndex = 0;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string firstName = FirstNameBox.Text.Trim();
            string middleName = MiddleNameBox.Text.Trim();
            string password = PasswordBox.Text.Trim();
            int roleId = (int)((ComboBoxItem)RoleCombo.SelectedItem).Tag;
            bool isActive = ((ComboBoxItem)StatusCombo.SelectedItem).Tag.ToString() == "True";

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
            {
                MessageBox.Show("Заполните логин, фамилию и имя");
                return;
            }

            var db = App.GetService<AppDbContext>();

            // Проверка: это текущий админ и ему меняют роль
            if (_userId == App.CurrentUser?.UserId && roleId != 1)
            {
                MessageBox.Show("Нельзя изменить свою роль администратора", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка: последний админ
            if (_userId > 0)
            {
                var existingUser = await db.Users.FindAsync(_userId);
                if (existingUser != null && existingUser.RoleId == 1 && roleId != 1)
                {
                    var adminCount = await db.Users.CountAsync(u => u.IsActive == true && u.RoleId == 1);
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("Нельзя изменить роль последнего администратора", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            if (_userId == 0)
            {
                if (string.IsNullOrEmpty(password)) { MessageBox.Show("Введите пароль"); return; }
                db.Users.Add(new User
                {
                    Login = login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    LastName = lastName,
                    FirstName = firstName,
                    MiddleName = middleName,
                    RoleId = roleId,
                    IsActive = isActive
                });
            }
            else
            {
                var user = await db.Users.FindAsync(_userId);
                if (user != null)
                {
                    user.Login = login;
                    user.LastName = lastName;
                    user.FirstName = firstName;
                    user.MiddleName = middleName;
                    user.RoleId = roleId;
                    user.IsActive = isActive;
                    if (!string.IsNullOrEmpty(password))
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                }
            }

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
    }
}