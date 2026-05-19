using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SimbirSoftCodeAnalyzer.Managers
{
    public static class SizeManager
    {
        private static string _currentSize = "Medium";

        public static string CurrentSize
        {
            get => _currentSize;
            private set
            {
                if (_currentSize != value)
                {
                    _currentSize = value;
                    OnStaticPropertyChanged();
                    OnStaticPropertyChanged(nameof(IsSmallSize));
                    OnStaticPropertyChanged(nameof(IsMediumSize));
                    OnStaticPropertyChanged(nameof(IsLargeSize));
                }
            }
        }

        public static bool IsSmallSize => CurrentSize == "Small";
        public static bool IsMediumSize => CurrentSize == "Medium";
        public static bool IsLargeSize => CurrentSize == "Large";

        public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

        private static void OnStaticPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }

        public static void ApplySize(string size)
        {
            CurrentSize = size;
            ApplySizeToApplication();
        }

        public static void ApplySizeToApplication()
        {
            var resources = Application.Current.Resources;

            switch (CurrentSize)
            {
                case "Small":
                    resources["DefaultIconWidth"] = 16.0;
                    resources["DefaultIconHeight"] = 16.0;
                    resources["ButtonIconWidth"] = 24.0;
                    resources["ButtonIconHeight"] = 24.0;
                    resources["AppFontSizeBig"] = 28.0;
                    resources["AppFontSizeH1"] = 20.0;
                    resources["AppFontSizeH2"] = 18.0;
                    resources["AppFontSizeH3"] = 16.0;
                    resources["AppFontSizeH4"] = 14.0;
                    resources["WindowFormFontSize"] = 12.0;
                    break;
                case "Large":
                    resources["DefaultIconWidth"] = 24.0;
                    resources["DefaultIconHeight"] = 24.0;
                    resources["ButtonIconWidth"] = 36.0;
                    resources["ButtonIconHeight"] = 36.0;
                    resources["AppFontSizeBig"] = 44.0;
                    resources["AppFontSizeH1"] = 28.0;
                    resources["AppFontSizeH2"] = 26.0;
                    resources["AppFontSizeH3"] = 24.0;
                    resources["AppFontSizeH4"] = 22.0;
                    resources["WindowFormFontSize"] = 16.0;
                    break;
                default: // Medium
                    resources["DefaultIconWidth"] = 20.0;
                    resources["DefaultIconHeight"] = 20.0;
                    resources["ButtonIconWidth"] = 30.0;
                    resources["ButtonIconHeight"] = 30.0;
                    resources["AppFontSizeBig"] = 36.0;
                    resources["AppFontSizeH1"] = 24.0;
                    resources["AppFontSizeH2"] = 22.0;
                    resources["AppFontSizeH3"] = 20.0;
                    resources["AppFontSizeH4"] = 18.0;
                    resources["WindowFormFontSize"] = 14.0;
                    break;
            }
        }

        public static void InitializeSize()
        {
            ApplySizeToApplication();
        }
    }
}