using System;
using Core.Services;
using SimbirSoftCodeAnalyzer.Managers;
using System.Windows;
using System.Windows.Input;
using SimbirSoftCodeAnalyzer.Views;

namespace SimbirSoftCodeAnalyzer.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly UserService _userService;
        private string _login = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;
        private Visibility _errorVisibility = Visibility.Collapsed;
        private Visibility _progressVisibility = Visibility.Collapsed;

        public LoginViewModel(UserService userService)
        {
            _userService = userService;
            LoginCommand = new AsyncRelayCommand(LoginAsync);
        }

        public string Login
        {
            get => _login;
            set => SetProperty(ref _login, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    IsButtonEnabled = !value;
                    IsProgressVisible = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private bool _isButtonEnabled = true;
        public bool IsButtonEnabled
        {
            get => _isButtonEnabled;
            set => SetProperty(ref _isButtonEnabled, value);
        }

        private Visibility _isProgressVisibleField = Visibility.Collapsed;
        public Visibility IsProgressVisible
        {
            get => _isProgressVisibleField;
            set => SetProperty(ref _isProgressVisibleField, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    IsErrorVisible = string.IsNullOrWhiteSpace(value)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }

        public Visibility IsErrorVisible
        {
            get => _errorVisibility;
            set => SetProperty(ref _errorVisibility, value);
        }

        public ICommand LoginCommand { get; }
        public string Password { get; set; } = string.Empty;

        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Login))
            {
                ErrorMessage = "Введите логин";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введите пароль";
                return;
            }

            IsLoading = true;
            try
            {
                var user = await _userService.LoginAsync(Login, Password);
                if (user == null)
                {
                    ErrorMessage = "Неверный логин или пароль";
                    return;
                }

                App.CurrentUser = user;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    Application.Current.Windows[0]?.Close();
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка подключения: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
