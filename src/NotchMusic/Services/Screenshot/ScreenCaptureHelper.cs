using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace NotchMusic.Services.Screenshot;

public static class ScreenCaptureHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint PW_CLIENTONLY = 0x00000001;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public static Bitmap CaptureVirtualScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    public static Bitmap? CaptureRegion(Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    public static Bitmap? CaptureWindowDirectly(IntPtr hWnd, Rectangle fallbackBounds)
    {
        if (hWnd == IntPtr.Zero) return null;

        RECT rect;
        int dwmRes = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
        int width = (dwmRes == 0 && rect.Width > 0) ? rect.Width : fallbackBounds.Width;
        int height = (dwmRes == 0 && rect.Height > 0) ? rect.Height : fallbackBounds.Height;

        if (width <= 0 || height <= 0)
        {
            if (GetWindowRect(hWnd, out rect) && rect.Width > 0 && rect.Height > 0)
            {
                width = rect.Width;
                height = rect.Height;
            }
            else
            {
                return null;
            }
        }

        // Coba gunakan PrintWindow dengan PW_RENDERFULLCONTENT agar form lain di atasnya TIDAK IKUT TER-CAPTURE
        try
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    bool success = PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT);
                    if (!success)
                    {
                        success = PrintWindow(hWnd, hdc, 0);
                    }

                    if (success)
                    {
                        g.ReleaseHdc(hdc);
                        hdc = IntPtr.Zero;
                        return bmp;
                    }
                }
                finally
                {
                    if (hdc != IntPtr.Zero)
                    {
                        try { g.ReleaseHdc(hdc); } catch { }
                    }
                }
            }
            bmp.Dispose();
        }
        catch { }

        // Fallback: jika PrintWindow gagal, ambil dari koordinat layar
        if (fallbackBounds.Width > 0 && fallbackBounds.Height > 0)
        {
            return CaptureRegion(fallbackBounds);
        }

        return null;
    }

    public static Bitmap? CaptureActiveWindow()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;

        RECT rect;
        int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
        if (result != 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            if (!GetWindowRect(hWnd, out rect) || rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }
        }

        var bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
        return CaptureWindowDirectly(hWnd, bounds);
    }

    public static (IntPtr hWnd, Rectangle bounds, string title) FindTopLevelWindowAtPoint(int screenX, int screenY, IntPtr ignoreHwnd)
    {
        IntPtr foundHwnd = IntPtr.Zero;
        Rectangle foundBounds = Rectangle.Empty;
        string foundTitle = string.Empty;

        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == ignoreHwnd) return true;
            if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

            RECT rect;
            int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
            if (result != 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                if (!GetWindowRect(hWnd, out rect) || rect.Width <= 0 || rect.Height <= 0)
                {
                    return true;
                }
            }

            var r = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
            if (r.Contains(screenX, screenY))
            {
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();

                if (r.Width >= 60 && r.Height >= 60)
                {
                    foundHwnd = hWnd;
                    foundBounds = r;
                    foundTitle = title;
                    return false; // Found topmost window under point
                }
            }
            return true;
        }, IntPtr.Zero);

        return (foundHwnd, foundBounds, foundTitle);
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle rect)
    {
        // Clamp rect inside source bounds
        int x = Math.Max(0, rect.X);
        int y = Math.Max(0, rect.Y);
        int w = Math.Min(rect.Width, source.Width - x);
        int h = Math.Min(rect.Height, source.Height - y);

        if (w <= 0 || h <= 0)
        {
            return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        }

        var target = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(target))
        {
            g.DrawImage(source, new Rectangle(0, 0, w, h), x, y, w, h, GraphicsUnit.Pixel);
        }
        return target;
    }

    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public static Bitmap ToBitmap(BitmapSource bitmapSource)
    {
        using var outStream = new MemoryStream();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmapSource));
        enc.Save(outStream);
        outStream.Seek(0, SeekOrigin.Begin);
        return new Bitmap(outStream);
    }

    public static void SaveBitmapSourceToPng(BitmapSource source, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    public static BitmapImage LoadBitmapImageWithoutLock(string filePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new Uri(filePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static void CopyToClipboardSafely(BitmapSource source)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                System.Windows.Clipboard.SetImage(source);
                return;
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
    }

    public static void PlayShutterSound()
    {
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch { }
    }
}
