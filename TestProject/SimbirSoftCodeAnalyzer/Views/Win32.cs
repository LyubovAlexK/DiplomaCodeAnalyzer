using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimbirSoftCodeAnalyzer.Views
{
    public enum ResizeDirection
    {
        Left = 61441,
        Right = 61442,
        Top = 61443,
        TopLeft = 61444,
        TopRight = 61445,
        Bottom = 61446,
        BottomLeft = 61447,
        BottomRight = 61448,
    }

    public static class Win32
    {
        public const int WM_NCLBUTTONDOWN = 0x00A1;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public static void WmNcLButtonDown(this Window window, int wParam, int lParam)
        {
            var helper = new WindowInteropHelper(window);
            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, wParam, lParam);
        }
    }
}