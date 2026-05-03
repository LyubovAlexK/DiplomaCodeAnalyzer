using SimbirSoftCodeAnalyzer.Views.Pages;
using SimbirSoftCodeAnalyzer.Views.Pages.Admin;
using SimbirSoftCodeAnalyzer.Views.Pages.Mentor;
using SimbirSoftCodeAnalyzer.Views.Pages.Trainee;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AdminHomePage = SimbirSoftCodeAnalyzer.Views.Pages.Admin.AdminHomePage;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupMenuForRole();
            ShowHomePage();
        }

        private void SetupMenuForRole()
        {
            var roleId = App.CurrentUser?.RoleId;

            // Скрываем всё
            BtnUsers.Visibility = Visibility.Collapsed;
            BtnSystemResources.Visibility = Visibility.Collapsed;
            BtnCheckManagement.Visibility = Visibility.Collapsed;
            BtnResults.Visibility = Visibility.Collapsed;
            BtnMyCode.Visibility = Visibility.Collapsed;
            BtnMyResults.Visibility = Visibility.Collapsed;

            switch (roleId)
            {
                case 1: // Админ
                    BtnUsers.Visibility = Visibility.Visible;
                    BtnSystemResources.Visibility = Visibility.Visible;
                    break;
                case 2: // Наставник
                    BtnCheckManagement.Visibility = Visibility.Visible;
                    BtnResults.Visibility = Visibility.Visible;
                    break;
                case 3: // Стажёр
                    BtnMyCode.Visibility = Visibility.Visible;
                    BtnMyResults.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ShowHomePage()
        {
            var roleId = App.CurrentUser?.RoleId;
            UserControl page = roleId switch
            {
                1 => new AdminHomePage(),
                2 => new MentorHomePage(),
                3 => new TraineeHomePage(),
                _ => new AdminHomePage()
            };
            ContentArea.Content = page;
            SetActiveButton("Home");
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                UserControl? page = tag switch
                {
                    "Home" => null,
                    "Users" => new UsersPage(),
                    "SystemResources" => new SystemResourcesPage(),
                    "CheckManagement" => new CheckManagementPage(),
                    "Results" => new ResultsLearningPage(),
                    "MyCode" => new MyCodePage(),
                    "MyResults" => new MyResultsPage(),
                    "Settings" => new SettingsPage(),
                    _ => null
                };

                if (page != null)
                {
                    ContentArea.Content = page;
                    SetActiveButton(tag);
                }
                else if (tag == "Home")
                {
                    ShowHomePage();
                }
            }
        }

        private void SetActiveButton(string tag)
{
    var allButtons = new[] { BtnHome, BtnUsers, BtnSystemResources, BtnCheckManagement, BtnResults, BtnMyCode, BtnMyResults, BtnSettings };

    foreach (var btn in allButtons)
    {
        if (btn.Tag?.ToString() == tag)
        {
            btn.Style = (Style)FindResource("SidebarActiveItemButton");
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Style = (Style)FindResource("SidebarItemButton");
            btn.Foreground = (Brush)FindResource("SecondaryTextBrush");
        }
    }
}

        // Управление окном
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentUser = null;
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}