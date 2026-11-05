using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NotchMusic.Helpers;

public static class WindowMonitorHelper
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;

    private static readonly uint CurrentPid = (uint)Process.GetCurrentProcess().Id;

    /// <summary>
    /// Menghitung jumlah jendela aplikasi nyata milik user selain DRANOTCH yang sedang terbuka dan tidak diminimize.
    /// </summary>
    public static int CountOtherOpenWindows()
    {
        int count = 0;
        var classNameBuilder = new StringBuilder(256);

        EnumWindows((hWnd, lParam) =>
        {
            try
            {
                // 1. Harus visible
                if (!IsWindowVisible(hWnd)) return true;

                // 2. Jika diminimize ke taskbar, tidak menutupi layar -> abaikan
                if (IsIconic(hWnd)) return true;

                // 3. Abaikan proses DRANOTCH sendiri
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == CurrentPid || pid == 0) return true;

                // 4. Harus memiliki judul window yang valid (bukan kosong atau Program Manager)
                int textLen = GetWindowTextLength(hWnd);
                if (textLen <= 0) return true;

                var titleBuilder = new StringBuilder(textLen + 1);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString();
                if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                // 5. Abaikan window yang cloaked oleh Windows (virtual desktop lain / background UWP)
                int cloaked = 0;
                if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0)
                {
                    return true;
                }

                // 6. Abaikan tool window kecil
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

                // 7. Harus top-level window (tidak memiliki owner)
                IntPtr owner = GetWindow(hWnd, GW_OWNER);
                if (owner != IntPtr.Zero) return true;

                // 8. Cek ukuran window di layar (bukan window dummy 0x0 atau sangat kecil)
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    if (width < 200 || height < 150) return true;
                }

                // 9. Abaikan window sistem Windows
                classNameBuilder.Clear();
                GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();

                if (className == "Progman" ||
                    className == "WorkerW" ||
                    className == "Shell_TrayWnd" ||
                    className == "Shell_SecondaryTrayWnd" ||
                    className == "EdgeUiInputTopWndClass")
                {
                    return true;
                }

                count++;
            }
            catch
            {
                // Ignore any native access issues for protected processes
            }

            return true;
        }, IntPtr.Zero);

        return count;
    }
}
