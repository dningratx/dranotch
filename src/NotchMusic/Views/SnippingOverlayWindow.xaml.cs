using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NotchMusic.Models;
using NotchMusic.Services.Screenshot;
using NotchMusic.Services.Settings;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace NotchMusic.Views;

public partial class SnippingOverlayWindow : Window
{
    private readonly IScreenshotService _screenshotService;
    private readonly ISettingsService _settingsService;
    private readonly Action? _onFinished;

    private Bitmap? _frozenBitmap;
    private System.Windows.Media.Imaging.BitmapSource? _frozenBitmapSource;
    private bool _isSelecting;
    private Point _startPoint;
    private Rect _currentRect;
    private bool _multiSnipEnabled;
    private int _capturedCount;
    private DispatcherTimer? _toastTimer;

    private SnipMode _currentMode = SnipMode.Rectangular;
    private IntPtr _hoveredWindowHwnd = IntPtr.Zero;
    private Rectangle _hoveredWindowBounds = Rectangle.Empty;
    private string _hoveredWindowTitle = string.Empty;

    public SnippingOverlayWindow(
        IScreenshotService screenshotService,
        ISettingsService settingsService,
        SnipMode initialMode = SnipMode.Rectangular,
        Action? onFinished = null)
    {
        InitializeComponent();
        _screenshotService = screenshotService;
        _settingsService = settingsService;
        _onFinished = onFinished;
        _currentMode = initialMode;

        _multiSnipEnabled = _settingsService.CurrentSettings.ScreenshotMultiSnipDefault;
        UpdateMultiSnipButton();

        Loaded += SnippingOverlayWindow_Loaded;
        KeyDown += SnippingOverlayWindow_KeyDown;
        MouseDown += SnippingOverlayWindow_MouseDown;
        MouseMove += SnippingOverlayWindow_MouseMove;
        MouseUp += SnippingOverlayWindow_MouseUp;
    }

    private void SnippingOverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Fit window to virtual screen bounds across all monitors
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        Left = virtualScreen.Left / dpiX;
        Top = virtualScreen.Top / dpiY;
        Width = virtualScreen.Width / dpiX;
        Height = virtualScreen.Height / dpiY;

        ScreenGeometry.Rect = new Rect(0, 0, Width, Height);

        CaptureScreenAndDisplay();
        SetSnipMode(_currentMode);

        Activate();
        Focus();
    }

    private void CaptureScreenAndDisplay()
    {
        try
        {
            _frozenBitmap = ScreenCaptureHelper.CaptureVirtualScreen();
            _frozenBitmapSource = ScreenCaptureHelper.ToBitmapSource(_frozenBitmap);
            FrozenImage.Source = _frozenBitmapSource;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal mengambil tangkapan layar: {ex.Message}", "DRANOTCH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public void SetSnipMode(SnipMode mode)
    {
        _currentMode = mode;
        _currentRect = Rect.Empty;
        UpdateSelectionUI();

        var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 255));
        var inactiveBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 38, 52));
        var activeFg = Brushes.Black;
        var inactiveFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 212, 224));

        BtnRectSnip.Background = mode == SnipMode.Rectangular ? activeBg : inactiveBg;
        BtnRectSnip.Foreground = mode == SnipMode.Rectangular ? activeFg : inactiveFg;

        BtnWindowSnip.Background = mode == SnipMode.Window ? activeBg : inactiveBg;
        BtnWindowSnip.Foreground = mode == SnipMode.Window ? activeFg : inactiveFg;

        BtnFullScreenSnip.Background = mode == SnipMode.FullScreen ? activeBg : inactiveBg;
        BtnFullScreenSnip.Foreground = mode == SnipMode.FullScreen ? activeFg : inactiveFg;

        if (mode == SnipMode.Window)
        {
            Cursor = Cursors.Hand;
            WindowModeHintBanner.Visibility = Visibility.Visible;
        }
        else
        {
            Cursor = Cursors.Cross;
            WindowModeHintBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void SnippingOverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FinishAndClose();
        }
        else if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            CaptureEntireScreen();
        }
        else if (e.Key == Key.M)
        {
            ToggleMultiSnip();
        }
        else if (e.Key == Key.W)
        {
            SetSnipMode(SnipMode.Window);
        }
        else if (e.Key == Key.R)
        {
            SetSnipMode(SnipMode.Rectangular);
        }
    }

    private bool IsOverToolbar(Point pt)
    {
        if (TopToolbar.Visibility != Visibility.Visible) return false;
        var relative = TopToolbar.TranslatePoint(new Point(0, 0), this);
        var toolbarRect = new Rect(relative.X, relative.Y, TopToolbar.ActualWidth, TopToolbar.ActualHeight);
        return toolbarRect.Contains(pt);
    }

    private void SnippingOverlayWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pt = e.GetPosition(this);
        if (IsOverToolbar(pt)) return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (_currentMode == SnipMode.Window)
            {
                // Capture the currently hovered window!
                if (_hoveredWindowBounds.Width > 0 && _hoveredWindowBounds.Height > 0)
                {
                    CaptureHoveredWindow();
                }
            }
            else if (_currentMode == SnipMode.Rectangular)
            {
                _isSelecting = true;
                _startPoint = pt;
                _currentRect = new Rect(_startPoint, _startPoint);
                UpdateSelectionUI();
                CaptureMouse();
            }
        }
    }

    private void SnippingOverlayWindow_MouseMove(object sender, MouseEventArgs e)
    {
        var cur = e.GetPosition(this);

        if (_currentMode == SnipMode.Window)
        {
            if (IsOverToolbar(cur))
            {
                _hoveredWindowBounds = Rectangle.Empty;
                _currentRect = Rect.Empty;
                UpdateSelectionUI();
                return;
            }

            TrackWindowUnderMouse(cur);
        }
        else if (_currentMode == SnipMode.Rectangular && _isSelecting)
        {
            double x = Math.Min(_startPoint.X, cur.X);
            double y = Math.Min(_startPoint.Y, cur.Y);
            double w = Math.Abs(cur.X - _startPoint.X);
            double h = Math.Abs(cur.Y - _startPoint.Y);

            _currentRect = new Rect(x, y, w, h);
            UpdateSelectionUI();
        }
    }

    private void TrackWindowUnderMouse(Point wpfPos)
    {
        if (_frozenBitmap == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        double scaleX = (double)_frozenBitmap.Width / ActualWidth;
        double scaleY = (double)_frozenBitmap.Height / ActualHeight;

        int screenPixelX = virtualScreen.Left + (int)Math.Round(wpfPos.X * scaleX);
        int screenPixelY = virtualScreen.Top + (int)Math.Round(wpfPos.Y * scaleY);

        var helper = new WindowInteropHelper(this);
        var (targetHwnd, targetBounds, targetTitle) = ScreenCaptureHelper.FindTopLevelWindowAtPoint(screenPixelX, screenPixelY, helper.Handle);

        if (targetHwnd != IntPtr.Zero && targetBounds.Width > 0 && targetBounds.Height > 0)
        {
            _hoveredWindowHwnd = targetHwnd;
            _hoveredWindowBounds = targetBounds;
            _hoveredWindowTitle = targetTitle;

            int relPixelX = targetBounds.X - virtualScreen.Left;
            int relPixelY = targetBounds.Y - virtualScreen.Top;

            double dipX = relPixelX / scaleX;
            double dipY = relPixelY / scaleY;
            double dipW = targetBounds.Width / scaleX;
            double dipH = targetBounds.Height / scaleY;

            _currentRect = new Rect(dipX, dipY, dipW, dipH);
            UpdateWindowSelectionUI(targetTitle, targetBounds.Width, targetBounds.Height);
        }
    }

    private void UpdateWindowSelectionUI(string title, int pixelW, int pixelH)
    {
        if (_currentRect.IsEmpty || _currentRect.Width <= 0 || _currentRect.Height <= 0)
        {
            SelectionGeometry.Rect = Rect.Empty;
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionGeometry.Rect = _currentRect;

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, _currentRect.X);
        Canvas.SetTop(SelectionBorder, _currentRect.Y);
        SelectionBorder.Width = _currentRect.Width;
        SelectionBorder.Height = _currentRect.Height;

        DimensionBadge.Visibility = Visibility.Visible;
        double badgeX = _currentRect.X + 8;
        double badgeY = _currentRect.Y + 8;
        if (badgeY < 80) badgeY = _currentRect.Y + _currentRect.Height - 34;

        Canvas.SetLeft(DimensionBadge, Math.Max(10, badgeX));
        Canvas.SetTop(DimensionBadge, Math.Max(10, badgeY));

        string displayTitle = string.IsNullOrWhiteSpace(title) ? "Jendela / Form" : (title.Length > 28 ? title[..25] + "..." : title);
        DimensionText.Text = $"🪟 {displayTitle} ({pixelW} × {pixelH} px) • Klik untuk tangkap";
    }

    private void SnippingOverlayWindow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentMode == SnipMode.Rectangular && _isSelecting)
        {
            _isSelecting = false;
            ReleaseMouseCapture();

            if (_currentRect.Width >= 10 && _currentRect.Height >= 10)
            {
                CaptureSelectedRect(_currentRect);
            }
            else
            {
                _currentRect = Rect.Empty;
                UpdateSelectionUI();
            }
        }
    }

    private void UpdateSelectionUI()
    {
        if (_currentRect.IsEmpty || _currentRect.Width <= 0 || _currentRect.Height <= 0)
        {
            SelectionGeometry.Rect = Rect.Empty;
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionGeometry.Rect = _currentRect;

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, _currentRect.X);
        Canvas.SetTop(SelectionBorder, _currentRect.Y);
        SelectionBorder.Width = _currentRect.Width;
        SelectionBorder.Height = _currentRect.Height;

        DimensionBadge.Visibility = Visibility.Visible;
        double badgeX = _currentRect.X + 4;
        double badgeY = _currentRect.Y + _currentRect.Height + 6;
        if (badgeY + 25 > ActualHeight)
        {
            badgeY = Math.Max(0, _currentRect.Y - 26);
        }

        Canvas.SetLeft(DimensionBadge, badgeX);
        Canvas.SetTop(DimensionBadge, badgeY);

        if (_frozenBitmap != null && ActualWidth > 0 && ActualHeight > 0)
        {
            double scaleX = (double)_frozenBitmap.Width / ActualWidth;
            double scaleY = (double)_frozenBitmap.Height / ActualHeight;
            int pw = (int)Math.Round(_currentRect.Width * scaleX);
            int ph = (int)Math.Round(_currentRect.Height * scaleY);
            DimensionText.Text = $"{pw} × {ph} px";
        }
        else
        {
            DimensionText.Text = $"{(int)_currentRect.Width} × {(int)_currentRect.Height} px";
        }
    }

    private void CaptureHoveredWindow()
    {
        if (_hoveredWindowBounds.Width <= 0 || _hoveredWindowBounds.Height <= 0) return;

        Bitmap? targetBmp = null;
        if (_hoveredWindowHwnd != IntPtr.Zero)
        {
            // Ambil langsung via PrintWindow agar jendela/form lain yang menutupi di atasnya TIDAK IKUT KECAPTURE
            targetBmp = ScreenCaptureHelper.CaptureWindowDirectly(_hoveredWindowHwnd, _hoveredWindowBounds);
        }

        if (targetBmp == null && _frozenBitmap != null)
        {
            var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
            int relPixelX = _hoveredWindowBounds.X - virtualScreen.Left;
            int relPixelY = _hoveredWindowBounds.Y - virtualScreen.Top;
            targetBmp = ScreenCaptureHelper.CropBitmap(_frozenBitmap, new Rectangle(relPixelX, relPixelY, _hoveredWindowBounds.Width, _hoveredWindowBounds.Height));
        }

        if (targetBmp == null) return;

        using (targetBmp)
        {
            string title = string.IsNullOrWhiteSpace(_hoveredWindowTitle) ? "Tangkapan Jendela" : $"Tangkapan: {_hoveredWindowTitle}";
            var item = _screenshotService.SaveScreenshot(targetBmp, "ActiveWindow", title);
            ProcessPostCapture(item, targetBmp);
        }
    }

    private void CaptureSelectedRect(Rect rect)
    {
        if (_frozenBitmap == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        double scaleX = (double)_frozenBitmap.Width / ActualWidth;
        double scaleY = (double)_frozenBitmap.Height / ActualHeight;

        int px = (int)Math.Round(rect.X * scaleX);
        int py = (int)Math.Round(rect.Y * scaleY);
        int pw = (int)Math.Round(rect.Width * scaleX);
        int ph = (int)Math.Round(rect.Height * scaleY);

        using var cropped = ScreenCaptureHelper.CropBitmap(_frozenBitmap, new Rectangle(px, py, pw, ph));
        var item = _screenshotService.SaveScreenshot(cropped, "Region");

        ProcessPostCapture(item, cropped);
    }

    private void CaptureEntireScreen()
    {
        if (_frozenBitmap == null) return;

        var item = _screenshotService.SaveScreenshot(_frozenBitmap, "FullScreen", "Tangkapan Seluruh Layar");
        ProcessPostCapture(item, _frozenBitmap);
    }

    private void ProcessPostCapture(ScreenshotItem item, Bitmap bmp)
    {
        _capturedCount++;

        // Clipboard
        if (_settingsService.CurrentSettings.ScreenshotAutoCopyToClipboard)
        {
            try
            {
                var src = ScreenCaptureHelper.ToBitmapSource(bmp);
                ScreenCaptureHelper.CopyToClipboardSafely(src);
            }
            catch { }
        }

        // Shutter feedback sound
        if (_settingsService.CurrentSettings.ScreenshotPlaySound)
        {
            ScreenCaptureHelper.PlayShutterSound();
        }

        // Reset current selection
        _currentRect = Rect.Empty;
        UpdateSelectionUI();

        if (_multiSnipEnabled)
        {
            SnipCountBadge.Visibility = Visibility.Visible;
            SnipCountText.Text = $"📸 {_capturedCount} Tersimpan";
            BtnDone.Visibility = Visibility.Visible;

            ShowToast($"📸 Tangkapan #{_capturedCount} tersimpan ke galeri & disalin ke clipboard!");
        }
        else
        {
            FinishAndClose();
        }
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            ToastNotification.Visibility = Visibility.Collapsed;
        };
        _toastTimer.Start();
    }

    private void ToggleMultiSnip()
    {
        _multiSnipEnabled = !_multiSnipEnabled;
        _settingsService.CurrentSettings.ScreenshotMultiSnipDefault = _multiSnipEnabled;
        _settingsService.Save();
        UpdateMultiSnipButton();
    }

    private void UpdateMultiSnipButton()
    {
        if (_multiSnipEnabled)
        {
            BtnMultiSnip.Content = "⚡ Multi-Snip: AKTIF";
            BtnMultiSnip.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            BtnMultiSnip.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            BtnMultiSnip.Content = "⚡ Multi-Snip: Nonaktif";
            BtnMultiSnip.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 38, 52));
            BtnMultiSnip.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 212, 224));
        }
    }

    private void FinishAndClose()
    {
        _toastTimer?.Stop();
        Close();
        _onFinished?.Invoke();
    }

    private void BtnRectSnip_Click(object sender, RoutedEventArgs e)
    {
        SetSnipMode(SnipMode.Rectangular);
    }

    private void BtnFullScreenSnip_Click(object sender, RoutedEventArgs e)
    {
        CaptureEntireScreen();
    }

    private void BtnWindowSnip_Click(object sender, RoutedEventArgs e)
    {
        SetSnipMode(SnipMode.Window);
    }

    private void BtnMultiSnip_Click(object sender, RoutedEventArgs e)
    {
        ToggleMultiSnip();
    }

    private void BtnDone_Click(object sender, RoutedEventArgs e)
    {
        FinishAndClose();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        FinishAndClose();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _frozenBitmap?.Dispose();
        _frozenBitmap = null;
    }
}
