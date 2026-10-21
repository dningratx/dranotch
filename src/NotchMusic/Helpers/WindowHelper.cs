using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NotchMusic.Helpers;

public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    public static void SetToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
    }

    public static void EnsureInVisibleScreen(Window window)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)window.Left, (int)window.Top));
        var workingArea = screen.WorkingArea;

        // Convert WinForms pixels to WPF DIPs
        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = workingArea.Left / dpiX;
        double areaTop = workingArea.Top / dpiY;
        double areaWidth = workingArea.Width / dpiX;
        double areaHeight = workingArea.Height / dpiY;

        if (window.Left < areaLeft || window.Left + window.ActualWidth > areaLeft + areaWidth)
        {
            window.Left = areaLeft + (areaWidth - window.ActualWidth) / 2.0;
        }

        if (window.Top < areaTop || window.Top + window.ActualHeight > areaTop + areaHeight)
        {
            window.Top = areaTop + 10;
        }
    }
}
