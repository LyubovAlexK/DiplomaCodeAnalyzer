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
        private int _userId;
        private readonly User? _user;

        public UserDialog(User? user = null)
        {
            InitializeComponent();
            _user = user;
            _userId = user?.UserId ?? 0;
            Loaded += async (s, e) => await LoadRolesAsync();
        }

        private async System.Threading.Tasks.Task LoadRolesAsync()
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

            var mentors = await db.Users.Where(u => u.RoleId == 2 && u.IsActive == true).ToListAsync();
            MentorCombo.Items.Clear();
            foreach (var m in mentors)
                MentorCombo.Items.Add(new ComboBoxItem { Content = $"{m.LastName} {m.FirstName}".Trim(), Tag = m.UserId });

            // Применяем данные пользователя
            if (_user != null)
            {
                LoginBox.Text = _user.Login;
                LastNameBox.Text = _user.LastName ?? "";
                FirstNameBox.Text = _user.FirstName ?? "";
                MiddleNameBox.Text = _user.MiddleName ?? "";

                // Скрываем выбор роли для администратора
                if (_user.RoleId == 1)
                {
                    RoleCombo.Visibility = Visibility.Collapsed;
                    RoleLabel.Visibility = Visibility.Collapsed;
                }

                // Блокируем смену роли, если это текущий пользователь
                if (_user.UserId == App.CurrentUser?.UserId)
                    RoleCombo.IsEnabled = false;

                // Статус
                foreach (ComboBoxItem item in StatusCombo.Items)
                    if (item.Tag.ToString() == (_user.IsActive == true ? "True" : "False"))
                        item.IsSelected = true;

                // Выбираем роль
                foreach (ComboBoxItem item in RoleCombo.Items)
                {
                    if (item.Tag is int roleId && roleId == _user.RoleId)
                    {
                        item.IsSelected = true;
                        bool isTrainee = _user.RoleId == 3;
                        MentorLabel.Visibility = isTrainee ? Visibility.Visible : Visibility.Collapsed;
                        MentorCombo.Visibility = isTrainee ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    }
                }

                // Выбираем наставника, если это стажёр
                if (_user.RoleId == 3)
                {
                    var project = await db.Projects
                        .FirstOrDefaultAsync(p => p.TraineeId == _user.UserId && p.IsArchived != true);
                    if (project != null)
                    {
                        foreach (ComboBoxItem item in MentorCombo.Items)
                        {
                            if (item.Tag is int mentorId && mentorId == project.CreatedBy)
                            {
                                item.IsSelected = true;
                                break;
                            }
                        }
                    }
                }

                Title = "Редактирование пользователя";
            }
            else
            {
                if (RoleCombo.Items.Count > 0) RoleCombo.SelectedIndex = 0;
                if (MentorCombo.Items.Count > 0) MentorCombo.SelectedIndex = 0;
                Title = "Добавление пользователя";
            }

            // Событие изменения роли
            RoleCombo.SelectionChanged += (s, e) =>
            {
                if (RoleCombo.SelectedItem is ComboBoxItem item && item.Tag is int roleId)
                {
                    bool isTrainee = roleId == 3;
                    MentorLabel.Visibility = isTrainee ? Visibility.Visible : Visibility.Collapsed;
                    MentorCombo.Visibility = isTrainee ? Visibility.Visible : Visibility.Collapsed;
                }
            };
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string firstName = FirstNameBox.Text.Trim();
            string middleName = MiddleNameBox.Text.Trim();
            string password = PasswordBox.Text.Trim();
            int roleId = (int)((ComboBoxItem)RoleCombo.SelectedItem).Tag;
            bool isActive = true;
            if (StatusCombo.SelectedItem is ComboBoxItem statusItem)
            {
                isActive = statusItem.Tag.ToString() == "True";
            }

            // Проверка обязательных полей
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
            {
                MessageBox.Show("Заполните логин, фамилию и имя");
                return;
            }

            // Валидация логина
            if (login.Length < 3)
            {
                MessageBox.Show("Логин должен содержать не менее 3 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!login.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.'))
            {
                MessageBox.Show("Логин может содержать только буквы, цифры, точку и подчёркивание", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Валидация пароля
            if (_userId == 0 && string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать не менее 6 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (password.Length > 50)
                {
                    MessageBox.Show("Пароль не должен превышать 50 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Валидация имени/фамилии
            if (lastName.Length < 2 || firstName.Length < 2)
            {
                MessageBox.Show("Имя и фамилия должны содержать не менее 2 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (lastName.Length > 50 || firstName.Length > 50)
            {
                MessageBox.Show("Имя и фамилия не должны превышать 50 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка на цифры в ФИО
            if (lastName.Any(c => char.IsDigit(c) || (!char.IsLetter(c) && c != '-' && c != '\'')))
            {
                MessageBox.Show("Фамилия может содержать только буквы, дефис и апостроф", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (firstName.Any(c => char.IsDigit(c) || (!char.IsLetter(c) && c != '-' && c != '\'')))
            {
                MessageBox.Show("Имя может содержать только буквы, дефис и апостроф", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrEmpty(middleName) && middleName.Any(c => char.IsDigit(c) || (!char.IsLetter(c) && c != '-' && c != '\'')))
            {
                MessageBox.Show("Отчество может содержать только буквы, дефис и апостроф", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var db = App.GetService<AppDbContext>();

            // Проверка уникальности логина
            var existingLogin = await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.UserId != _userId);
            if (existingLogin != null)
            {
                MessageBox.Show("Пользователь с таким логином уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка: текущий админ не может изменить свою роль
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

            // Проверка: стажёр должен иметь наставника
            if (roleId == 3 && MentorCombo.SelectedItem is not ComboBoxItem)
            {
                MessageBox.Show("Выберите наставника для стажёра", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Сохранение пользователя
            User savedUser;
            if (_userId == 0)
            {
                savedUser = new User
                {
                    Login = login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    LastName = lastName,
                    FirstName = firstName,
                    MiddleName = middleName,
                    RoleId = roleId,
                    IsActive = true
                };
                db.Users.Add(savedUser);
            }
            else
            {
                savedUser = await db.Users.FindAsync(_userId);
                if (savedUser != null)
                {
                    savedUser.Login = login;
                    savedUser.LastName = lastName;
                    savedUser.FirstName = firstName;
                    savedUser.MiddleName = middleName;
                    savedUser.RoleId = roleId;
                    savedUser.IsActive = isActive;
                    if (!string.IsNullOrEmpty(password))
                        savedUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                }
            }

            await db.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
    }
}