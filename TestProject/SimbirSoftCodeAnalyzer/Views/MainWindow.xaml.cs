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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        private bool _isSidebarCollapsed = false;
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupMenuForRole();
            ShowHomePage();
            UpdateSidebarBackground();
        }

        private void SetupMenuForRole()
        {
            var roleId = App.CurrentUser?.RoleId;

            BtnUsers.Visibility = Visibility.Collapsed;
            BtnSystemResources.Visibility = Visibility.Collapsed;
            BtnHistory.Visibility = Visibility.Collapsed;
            BtnMyCode.Visibility = Visibility.Collapsed;
            BtnMyResults.Visibility = Visibility.Collapsed;
            BtnResults.Visibility = Visibility.Collapsed;
            BtnReferences.Visibility = Visibility.Collapsed;
            BtnArchRules.Visibility = Visibility.Collapsed;
            BtnProjects.Visibility = Visibility.Collapsed;

            switch (roleId)
            {
                case 1: // Админ
                    BtnHome.Content = "Главное меню";
                    BtnUsers.Visibility = Visibility.Visible;
                    BtnSystemResources.Visibility = Visibility.Visible;
                    BtnSystemResources.Content = "Системные ресурсы";
                    break;
                case 2: // Наставник
                    BtnHome.Content = "Главное меню";
                    BtnSystemResources.Visibility = Visibility.Visible;
                    BtnSystemResources.Content = "Технические задания";
                    BtnResults.Visibility = Visibility.Visible;
                    BtnReferences.Visibility = Visibility.Visible;
                    BtnArchRules.Visibility = Visibility.Visible;
                    BtnProjects.Visibility = Visibility.Visible;
                    break;
                case 3: // Стажёр
                    BtnHome.Content = "Главное меню";
                    BtnMyCode.Visibility = Visibility.Visible;
                    BtnMyResults.Visibility = Visibility.Visible;
                    BtnHistory.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarCollapsed = true;
            SidebarColumn.Width = new GridLength(60);
            MenuPanel.Visibility = Visibility.Collapsed;
            LogoPanel.Visibility = Visibility.Collapsed;
            BottomPanel.Visibility = Visibility.Collapsed;
            ExpandPillButton.Visibility = Visibility.Visible;
        }

        private void ExpandSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarCollapsed = false;
            SidebarColumn.Width = new GridLength(280);
            MenuPanel.Visibility = Visibility.Visible;
            LogoPanel.Visibility = Visibility.Visible;
            BottomPanel.Visibility = Visibility.Visible;
            ExpandPillButton.Visibility = Visibility.Collapsed;
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
        public void UpdateSidebarBackground()
        {
            var bgColor = (Color)Application.Current.Resources["CardBackgroundColor"];
            SidebarPanel.Background = new SolidColorBrush(bgColor);
        }
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var roleId = App.CurrentUser?.RoleId;
            if (sender is Button button && button.Tag is string tag)
            {
                UserControl? page = tag switch
                {
                    "Home" => null,
                    "Users" => new UsersPage(),
                    "SystemResources" => roleId switch
                    {
                        1 => new SystemResourcesPage(),   // Админ → Системные ресурсы
                        2 => new SpecificationsPage(),    // Наставник → ТЗ и требования
                        _ => new SystemResourcesPage()
                    },
                    "CheckManagement" => new CheckManagementPage(),
                    "History" => new HistoryPage(),
                    "MyCode" => new MyCodePage(),
                    "MyResults" => new MyResultsPage(),
                    "Results" => new ResultsPage(),
                    "References" => new ReferencesPage(),
                    "ArchRules" => new ArchRulesPage(),
                    "Projects" => new ProjectsPage(),
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

        public void SetActiveButton(string tag)
        {
            var allButtons = new[] { BtnHome, BtnUsers, BtnSystemResources, BtnResults, BtnReferences, BtnArchRules, BtnHistory, BtnMyCode, BtnMyResults, BtnProjects, BtnSettings };

            foreach (var btn in allButtons)
            {
                if (btn.Tag?.ToString() == tag)
                {
                    btn.Style = (Style)FindResource("SidebarActiveItemButton");
                    btn.Foreground = Brushes.White;
                    SetIconColor(btn, "#FFFFFF");
                }
                else
                {
                    btn.Style = (Style)FindResource("SidebarItemButton");
                    btn.Foreground = (Brush)FindResource("SecondaryTextBrush");
                    SetIconColor(btn, "#8B96A0");
                }
            }
        }

        private void SetIconColor(Button button, string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var brush = new SolidColorBrush(color);

            // Ищем Rectangle внутри кнопки
            if (button.Content is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Rectangle rect)
                    {
                        rect.Fill = brush;
                        break;
                    }
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