using SimbirSoftCodeAnalyzer.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace SimbirSoftCodeAnalyzer.Managers
{
    public static class ThemeManager
    {
        private static string _currentColorTheme = "Blue";
        private static string _currentTheme = "Light";

        public static string CurrentColor => CurrentColorTheme;
        public static bool IsPurpleTheme => CurrentColorTheme == "Purple";
        public static bool IsYellowTheme => CurrentColorTheme == "Yellow";
        public static bool IsRedTheme => CurrentColorTheme == "Red";
        public static bool IsGreenTheme => CurrentColorTheme == "Green";
        public static bool IsBlueTheme => CurrentColorTheme == "Blue";
        public static bool IsDarkTheme => CurrentTheme == "Dark";
        public static bool IsLightTheme => CurrentTheme == "Light";

        public static string CurrentColorTheme
        {
            get => _currentColorTheme;
            private set
            {
                if (_currentColorTheme != value)
                {
                    _currentColorTheme = value;
                    OnStaticPropertyChanged();
                    OnStaticPropertyChanged(nameof(CurrentColor));
                    OnStaticPropertyChanged(nameof(IsPurpleTheme));
                    OnStaticPropertyChanged(nameof(IsYellowTheme));
                    OnStaticPropertyChanged(nameof(IsRedTheme));
                    OnStaticPropertyChanged(nameof(IsGreenTheme));
                    OnStaticPropertyChanged(nameof(IsBlueTheme));
                }
            }
        }

        public static string CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    OnStaticPropertyChanged();
                    OnStaticPropertyChanged(nameof(IsDarkTheme));
                    OnStaticPropertyChanged(nameof(IsLightTheme));
                }
            }
        }

        public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

        private static void OnStaticPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }

        public static void ApplyTheme(string theme)
        {
            CurrentTheme = theme;
            ApplyThemeToApplication();
        }

        public static void ApplyThemeToApplication()
        {
            var resources = Application.Current.Resources;

            if (IsDarkTheme)
            {
                resources["GlobalBackgroundColor"] = Color.FromRgb(23, 24, 26);
                resources["CardBackgroundColor"] = Color.FromRgb(32, 33, 35);
                resources["HeaderBackground"] = Color.FromRgb(32, 33, 35);
                resources["DarkTextColor"] = Color.FromRgb(247, 247, 249);
                resources["SecondaryTextColor"] = Color.FromRgb(184, 186, 191);
                resources["BorderColor"] = Color.FromRgb(51, 52, 55);
                resources["HoverColor"] = Color.FromRgb(51, 52, 55);
                resources["SidebarBackgroundColor"] = Color.FromRgb(15, 16, 16);
            }
            else
            {
                resources["GlobalBackgroundColor"] = Color.FromRgb(247, 247, 249);
                resources["CardBackgroundColor"] = Colors.White;
                resources["HeaderBackground"] = Colors.White;
                resources["DarkTextColor"] = Color.FromRgb(15, 16, 16);
                resources["SecondaryTextColor"] = Color.FromRgb(89, 90, 91);
                resources["BorderColor"] = Color.FromRgb(229, 231, 235);
                resources["HoverColor"] = Color.FromRgb(238, 242, 255);
                resources["SidebarBackgroundColor"] = Color.FromRgb(15, 16, 16);
            }

            UpdateBrushResources();
        }

        private static void UpdateBrushResources()
        {
            var resources = Application.Current.Resources;

            resources["GlobalBackgroundBrush"] = new SolidColorBrush((Color)resources["GlobalBackgroundColor"]);
            resources["CardBackgroundBrush"] = new SolidColorBrush((Color)resources["CardBackgroundColor"]);
            resources["HeaderBackgroundBrush"] = new SolidColorBrush((Color)resources["HeaderBackground"]);
            resources["DarkTextBrush"] = new SolidColorBrush((Color)resources["DarkTextColor"]);
            resources["SecondaryTextBrush"] = new SolidColorBrush((Color)resources["SecondaryTextColor"]);
            resources["BorderBrush"] = new SolidColorBrush((Color)resources["BorderColor"]);
            resources["HoverBrush"] = new SolidColorBrush((Color)resources["HoverColor"]);
            resources["Gray100Brush"] = new SolidColorBrush((Color)resources["GlobalBackgroundColor"]);

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateSidebarBackground();
            }
        }

        public static void InitializeTheme()
        {
            ApplyThemeToApplication();
        }
    }
}