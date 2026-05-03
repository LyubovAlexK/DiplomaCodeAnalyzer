using SimbirSoftCodeAnalyzer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();

            var userService = App.GetService<Core.Services.UserService>();
            _viewModel = new LoginViewModel(userService);
            DataContext = _viewModel;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        }
        private bool _isPasswordVisible = false;

        private void EyeIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                EyeIcon.Text = "🔓";
                PasswordTextBox.Focus();
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                EyeIcon.Text = "👁";

                if (DataContext is LoginViewModel vm)
                    vm.Password = PasswordBox.Password;
            }
        }
        //Зацикливание видео
        private void BackgroundVideo_Loaded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Opacity = 0;
            BackgroundVideo.MediaOpened += (s, args) =>
            {
                BackgroundVideo.Opacity = 1;
            };
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.FromMilliseconds(1);
            BackgroundVideo.Play();
        }
    }
}
