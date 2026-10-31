using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace NotchMusic.Views;

public partial class YouTubeMusicWindow : Window
{
    private const string YouTubeMusicUrl = "https://music.youtube.com/";
    private readonly MainViewModel? _viewModel;
    private readonly ISettingsService? _settingsService;
    private bool _isForceClose;
    private bool _isInitialized;
    private bool _isVocalRemoverEnabled;

    public YouTubeMusicWindow(MainViewModel? viewModel = null, ISettingsService? settingsService = null)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _isVocalRemoverEnabled = _viewModel?.IsVocalRemoverEnabled ?? false;
        InitializeComponent();

        try
        {
            var uri = new Uri("pack://application:,,,/NotchMusic;component/Assets/app.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }

        if (_settingsService != null)
        {
            var s = _settingsService.CurrentSettings;
            Topmost = s.YouTubeMusicAlwaysOnTop;
            // Jika masih memakai ukuran lama bawaan (1080x720), ubah ke default seukuran handphone (380x740)
            if (s.YouTubeMusicWidth == 1080 && s.YouTubeMusicHeight == 720)
            {
                s.YouTubeMusicWidth = 380;
                s.YouTubeMusicHeight = 740;
                _settingsService.Save();
            }
            if (s.YouTubeMusicWidth >= MinWidth && s.YouTubeMusicHeight >= MinHeight)
            {
                Width = s.YouTubeMusicWidth;
                Height = s.YouTubeMusicHeight;
            }
            if (s.YouTubeMusicLeft > 0 && s.YouTubeMusicTop >= 0)
            {
                Left = s.YouTubeMusicLeft;
                Top = s.YouTubeMusicTop;
            }
        }
        else
        {
            Width = 380;
            Height = 740;
        }

        UpdatePinVisuals();
        Loaded += YouTubeMusicWindow_Loaded;
    }

    private async void YouTubeMusicWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            StatusText.Text = "Menyiapkan browser...";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataFolder = Path.Combine(localAppData, "DRANOTCH", "WebView2Profile");
            var legacyProfile = Path.Combine(localAppData, "NotchMusic", "WebView2Profile");
            if (!Directory.Exists(userDataFolder) && Directory.Exists(legacyProfile))
            {
                try { Directory.CreateDirectory(Path.GetDirectoryName(userDataFolder)!); Directory.Move(legacyProfile, userDataFolder); } catch { }
            }
            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: options);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

            webView.CoreWebView2.WebMessageReceived += (s, ev) =>
            {
                try
                {
                    var msg = ev.TryGetWebMessageAsString();
                    if (msg == "yt_playing")
                    {
                        _viewModel?.NotifyYouTubeMusicPlaying();
                    }
                }
                catch { }
            };

            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    function hookMedia() {
                        var v = document.querySelector('video') || document.querySelector('audio');
                        if (v && !v.__dranotchHooked) {
                            v.__dranotchHooked = true;
                            v.addEventListener('play', function() {
                                if (window.chrome && window.chrome.webview) {
                                    window.chrome.webview.postMessage('yt_playing');
                                }
                            });
                        }
                    }
                    hookMedia();
                    setInterval(hookMedia, 1500);

                    document.addEventListener('click', function(e) {
                        try {
                            var target = e.target;
                            var btn = target.closest('#play-pause-button, button[aria-label*=""Play"" i], button[aria-label*=""Putar"" i]');
                            if (btn && window.chrome && window.chrome.webview) {
                                window.chrome.webview.postMessage('yt_playing');
                            }
                        } catch(err) {}
                    }, true);
                })();
            ");

            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                WebAudioVocalRemoverScript.GetBootstrapScript(_isVocalRemoverEnabled));

            webView.NavigationStarting += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Memuat...";
                });
            };

            webView.NavigationCompleted += async (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    webView.Visibility = Visibility.Visible;
                    StatusText.Text = "Tersambung ke DRANOTCH";
                    BackButton.IsEnabled = webView.CanGoBack;
                    ForwardButton.IsEnabled = webView.CanGoForward;
                });

                await ApplyVocalRemoverToWebViewAsync();
            };

            webView.Source = new Uri(YouTubeMusicUrl);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gagal memuat WebView2";
            System.Windows.MessageBox.Show(
                $"Gagal menginisialisasi Microsoft Edge WebView2:\n{ex.Message}\n\nPastikan Microsoft Edge WebView2 Runtime terpasang di sistem Anda.",
                "DRANOTCH - WebView2 Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public async Task SetVocalRemoverAsync(bool enabled)
    {
        _isVocalRemoverEnabled = enabled;
        await ApplyVocalRemoverToWebViewAsync();
    }

    private async Task ApplyVocalRemoverToWebViewAsync()
    {
        try
        {
            if (webView?.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(
                    WebAudioVocalRemoverScript.GetSetEnabledScript(_isVocalRemoverEnabled));
            }
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    DragMove();
                }
            }
            catch { }
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinVisuals();
        if (_settingsService != null)
        {
            _settingsService.CurrentSettings.YouTubeMusicAlwaysOnTop = Topmost;
            _settingsService.Save();
        }
    }

    private void UpdatePinVisuals()
    {
        if (PinButton == null) return;
        if (Topmost)
        {
            PinButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0033"));
            PinButton.Foreground = System.Windows.Media.Brushes.White;
            PinButton.ToolTip = "Mode Mengambang AKTIF (Selalu di atas). Klik untuk lepas.";
        }
        else
        {
            PinButton.Background = System.Windows.Media.Brushes.Transparent;
            PinButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            PinButton.ToolTip = "Mode Mengambang NONAKTIF. Klik agar selalu di atas.";
        }
    }

    private void PresetSizeButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181824")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#323246")),
            BorderThickness = new Thickness(1)
        };

        var presets = new (string Label, double W, double H)[]
        {
            ("📱 Handphone (380 × 740)", 380, 740),
            ("📱 Mini PiP (400 × 260)", 400, 260),
            ("📻 Ringkas (480 × 640)", 480, 640),
            ("💻 Sedang (640 × 720)", 640, 720),
            ("🖥️ Standar Layar Lebar (1080 × 720)", 1080, 720),
            ("🎬 Layar Lebar HD (1280 × 800)", 1280, 800)
        };

        foreach (var p in presets)
        {
            var item = new MenuItem
            {
                Header = p.Label,
                Foreground = System.Windows.Media.Brushes.White,
                Padding = new Thickness(10, 6, 10, 6)
            };
            item.Click += (s, ev) => SetWindowPreset(p.W, p.H);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D3E")) });

        var zoomInItem = new MenuItem
        {
            Header = "➕ Perbesar Ukuran (+15%)",
            Foreground = System.Windows.Media.Brushes.White,
            Padding = new Thickness(10, 6, 10, 6)
        };
        zoomInItem.Click += (s, ev) => ResizeDelta(1.15);
        menu.Items.Add(zoomInItem);

        var zoomOutItem = new MenuItem
        {
            Header = "➖ Perkecil Ukuran (-15%)",
            Foreground = System.Windows.Media.Brushes.White,
            Padding = new Thickness(10, 6, 10, 6)
        };
        zoomOutItem.Click += (s, ev) => ResizeDelta(0.85);
        menu.Items.Add(zoomOutItem);

        var resetItem = new MenuItem
        {
            Header = "🔍 Reset Ukuran Standar (1080 × 720)",
            Foreground = System.Windows.Media.Brushes.White,
            Padding = new Thickness(10, 6, 10, 6)
        };
        resetItem.Click += (s, ev) => SetWindowPreset(1080, 720);
        menu.Items.Add(resetItem);

        var zoomMenu = new MenuItem
        {
            Header = "🔎 Skala Konten (Zoom)",
            Foreground = System.Windows.Media.Brushes.White,
            Padding = new Thickness(10, 6, 10, 6)
        };
        var zoomLevels = new (string Label, double Factor)[]
        {
            ("75%", 0.75),
            ("90%", 0.90),
            ("100% (Normal)", 1.0),
            ("110%", 1.10),
            ("125%", 1.25)
        };
        foreach (var z in zoomLevels)
        {
            var zi = new MenuItem { Header = z.Label, Foreground = System.Windows.Media.Brushes.White, Padding = new Thickness(8, 4, 8, 4) };
            zi.Click += (s, ev) => { if (webView?.CoreWebView2 != null) webView.ZoomFactor = z.Factor; };
            zoomMenu.Items.Add(zi);
        }
        menu.Items.Add(zoomMenu);

        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private void SetWindowPreset(double targetWidth, double targetHeight)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }

        Width = Math.Max(MinWidth, targetWidth);
        Height = Math.Max(MinHeight, targetHeight);

        // Pastikan posisi jendela tetap dalam batas layar aktif
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
        var area = screen.WorkingArea;

        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = area.Left / dpiX;
        double areaTop = area.Top / dpiY;
        double areaRight = area.Right / dpiX;
        double areaBottom = area.Bottom / dpiY;

        if (Left + Width > areaRight) Left = Math.Max(areaLeft, areaRight - Width - 10);
        if (Top + Height > areaBottom) Top = Math.Max(areaTop, areaBottom - Height - 10);

        SaveWindowBounds();
    }

    private void ResizeDelta(double scaleFactor)
    {
        SetWindowPreset(Width * scaleFactor, Height * scaleFactor);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CanGoBack) webView.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CanGoForward) webView.GoForward();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        webView.Reload();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri(YouTubeMusicUrl);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (MaximizeButton != null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "🗗" : "🗖";
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Kembalikan Ukuran" : "Perbesar Jendela (Maximize)";
        }
        if (RootBorder != null)
        {
            RootBorder.Padding = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(5);
            RootBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(8);
            RootBorder.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1.5);
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SaveWindowBounds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SaveWindowBounds();
    }

    private void SaveWindowBounds()
    {
        if (!_isInitialized || WindowState != WindowState.Normal) return;
        if (_settingsService != null)
        {
            _settingsService.CurrentSettings.YouTubeMusicLeft = Left;
            _settingsService.CurrentSettings.YouTubeMusicTop = Top;
            _settingsService.CurrentSettings.YouTubeMusicWidth = Width;
            _settingsService.CurrentSettings.YouTubeMusicHeight = Height;
            _settingsService.Save();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Menutup jendela akan menyembunyikannya ke background agar musik tetap berputar
        Hide();
    }

    private void DismissTip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Parent is FrameworkElement parent && parent.Parent is FrameworkElement banner)
        {
            banner.Visibility = Visibility.Collapsed;
        }
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    public async Task PausePlaybackAsync()
    {
        try
        {
            if (webView?.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(
                    "(function() { var v = document.querySelector('video') || document.querySelector('audio'); if (v && !v.paused) { v.pause(); } })();"
                );
            }
        }
        catch { }
    }

    public void PauseAndHide()
    {
        _ = PausePlaybackAsync();
        if (IsVisible)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            // Jangan matikan proses pemutar, cukup sembunyikan agar audio tetap berputar
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
