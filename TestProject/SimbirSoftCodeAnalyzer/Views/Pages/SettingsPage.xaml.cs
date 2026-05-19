using System.Windows;
using System.Windows.Controls;
using SimbirSoftCodeAnalyzer.Managers;

namespace SimbirSoftCodeAnalyzer.Views.Pages
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            if (DarkThemeRadio == null || LightThemeRadio == null) return;

            if (ThemeManager.IsDarkTheme)
                DarkThemeRadio.IsChecked = true;
            else
                LightThemeRadio.IsChecked = true;

            if (SmallSizeRadio == null || MediumSizeRadio == null || LargeSizeRadio == null) return;

            if (SizeManager.IsSmallSize)
                SmallSizeRadio.IsChecked = true;
            else if (SizeManager.IsLargeSize)
                LargeSizeRadio.IsChecked = true;
            else
                MediumSizeRadio.IsChecked = true;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (DarkThemeRadio == null || LightThemeRadio == null) return;

            if (DarkThemeRadio.IsChecked == true)
                ThemeManager.ApplyTheme("Dark");
            else
                ThemeManager.ApplyTheme("Light");
        }

        private void SizeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (SmallSizeRadio == null || MediumSizeRadio == null || LargeSizeRadio == null) return;

            if (SmallSizeRadio.IsChecked == true)
                SizeManager.ApplySize("Small");
            else if (LargeSizeRadio.IsChecked == true)
                SizeManager.ApplySize("Large");
            else
                SizeManager.ApplySize("Medium");
        }
    }
}