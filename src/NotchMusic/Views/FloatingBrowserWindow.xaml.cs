using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ContextMenu = System.Windows.Controls.ContextMenu;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace NotchMusic.Views;

public partial class FloatingBrowserWindow : Window
{
    private const string DefaultUrl = "https://www.youtube.com/";
    private readonly MainViewModel? _viewModel;
    private readonly ISettingsService? _settingsService;
    private bool _isForceClose;
    private bool _isInitialized;
    private bool _isWindowedFullscreen;

    public FloatingBrowserWindow(MainViewModel? viewModel = null, ISettingsService? settingsService = null)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        InitializeComponent();

        try
        {
            var uri = new Uri("pack://application:,,,/NotchMusic;component/Assets/app.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }

        // Restore settings if available
        if (_settingsService != null)
        {
            var s = _settingsService.CurrentSettings;
            Topmost = s.BrowserAlwaysOnTop;
            if (s.BrowserWidth >= MinWidth && s.BrowserHeight >= MinHeight)
            {
                Width = s.BrowserWidth;
                Height = s.BrowserHeight;
            }
        }

        UpdatePinVisuals();
        Loaded += FloatingBrowserWindow_Loaded;
    }

    private async void FloatingBrowserWindow_Loaded(object sender, RoutedEventArgs e)
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
            var userDataFolder = Path.Combine(localAppData, "DRANOTCH", "FloatingBrowserProfile");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

            // Pastikan iframe dan video player (seperti di LK21/YouTube) diizinkan fullscreen
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                document.addEventListener('DOMContentLoaded', () => {
                    const iframes = document.querySelectorAll('iframe');
                    iframes.forEach(f => {
                        f.setAttribute('allowfullscreen', 'true');
                        f.setAttribute('webkitallowfullscreen', 'true');
                        f.setAttribute('mozallowfullscreen', 'true');
                    });
                });
            ");

            // Intersepsi HTML5 Fullscreen:
            // WebView2 otomatis membatasi fullscreen ke dalam batas kontrol jendela!
            // Kita sembunyikan address bar dan pertahankan drag bar di atas sehingga jendela
            // SELALU BISA DIGESER DAN DI-RESIZE, tidak pernah macet atau menutupi desktop.
            webView.CoreWebView2.ContainsFullScreenElementChanged += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    bool isFullscreen = webView.CoreWebView2.ContainsFullScreenElement;
                    ApplyWindowedFullscreen(isFullscreen);
                });
            };

            // Buka popup / link baru di dalam jendela yang sama
            webView.CoreWebView2.NewWindowRequested += (s, ev) =>
            {
                ev.Handled = true;
                webView.CoreWebView2.Navigate(ev.Uri);
            };

            webView.CoreWebView2.DocumentTitleChanged += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var title = webView.CoreWebView2.DocumentTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        StatusText.Text = title;
                        Title = $"{title} - DRANOTCH Browser";
                    }
                });
            };

            webView.NavigationStarting += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Memuat...";
                });
            };

            webView.NavigationCompleted += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    webView.Visibility = Visibility.Visible;
                    var title = webView.CoreWebView2.DocumentTitle;
                    StatusText.Text = string.IsNullOrWhiteSpace(title) ? "Siap" : title;
                    BackButton.IsEnabled = webView.CanGoBack;
                    ForwardButton.IsEnabled = webView.CanGoForward;

                    if (webView.Source != null)
                    {
                        AddressTextBox.Text = webView.Source.ToString();
                        if (_settingsService != null)
                        {
                            _settingsService.CurrentSettings.BrowserLastUrl = webView.Source.ToString();
                            _settingsService.Save();
                        }
                    }
                });
            };

            string initialUrl = DefaultUrl;
            if (_settingsService != null && !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.BrowserLastUrl))
            {
                initialUrl = _settingsService.CurrentSettings.BrowserLastUrl;
            }

            AddressTextBox.Text = initialUrl;
            webView.Source = new Uri(initialUrl);
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

    public void ApplyWindowedFullscreen(bool isFullscreen)
    {
        _isWindowedFullscreen = isFullscreen;
        if (isFullscreen)
        {
            // Sembunyikan bar navigasi URL agar video memenuhi jendela
            NavBarBorder.Visibility = Visibility.Collapsed;
            ToggleNavIcon.Text = "▼";
            // PENTING: Title bar dan grip "⋮⋮ Geser" TETAP AKTIF sehingga jendela tetap 100% bisa digeser dan di-resize!
        }
        else
        {
            NavBarBorder.Visibility = Visibility.Visible;
            ToggleNavIcon.Text = "▲";
        }
    }

    private void ToggleNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (NavBarBorder.Visibility == Visibility.Visible)
        {
            NavBarBorder.Visibility = Visibility.Collapsed;
            ToggleNavIcon.Text = "▼";
        }
        else
        {
            NavBarBorder.Visibility = Visibility.Visible;
            ToggleNavIcon.Text = "▲";
        }
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
            _settingsService.CurrentSettings.BrowserAlwaysOnTop = Topmost;
            _settingsService.Save();
        }
    }

    private void UpdatePinVisuals()
    {
        if (Topmost)
        {
            PinButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A2BE2"));
            PinButton.Foreground = Brushes.White;
            PinButton.ToolTip = "Mode Mengambang AKTIF (Selalu di atas). Klik untuk lepas.";
        }
        else
        {
            PinButton.Background = Brushes.Transparent;
            PinButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            PinButton.ToolTip = "Mode Mengambang NONAKTIF. Klik agar selalu di atas.";
        }
    }

    private void PresetSizeButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181824")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#323246")),
            BorderThickness = new Thickness(1)
        };

        var presets = new (string Label, double W, double H)[]
        {
            ("📱 Mini PiP (360 × 202 - 16:9)", 360, 202),
            ("📺 Kecil (480 × 270 - 16:9)", 480, 270),
            ("💻 Sedang (720 × 405 - 16:9)", 720, 405),
            ("🖥️ Standar (960 × 540 - 16:9)", 960, 540),
            ("🎬 Layar HD (1280 × 720 - 16:9)", 1280, 720)
        };

        foreach (var p in presets)
        {
            var item = new MenuItem
            {
                Header = p.Label,
                Foreground = Brushes.White,
                Padding = new Thickness(10, 6, 10, 6)
            };
            item.Click += (s, ev) => SetWindowPreset(p.W, p.H);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private void SetWindowPreset(double targetWidth, double targetHeight)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }

        Width = targetWidth;
        Height = targetHeight;

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

    private void NavigateToInputUrl()
    {
        var input = AddressTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        string targetUrl;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            targetUrl = uri.ToString();
        }
        else if (input.Contains('.') && !input.Contains(' '))
        {
            targetUrl = "https://" + input;
        }
        else
        {
            targetUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
        }

        try
        {
            webView.Source = new Uri(targetUrl);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Alamat URL tidak valid:\n{ex.Message}", "DRANOTCH Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToInputUrl();
        }
    }

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToInputUrl();
    }

    private void NavigateYouTube_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri("https://www.youtube.com");
    }

    private void NavigateYTMusic_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri("https://music.youtube.com");
    }

    private void NavigateLk21_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri("https://tv.layarkaca21.to/");
    }

    private void NavigateTwitch_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri("https://www.twitch.tv");
    }

    private void NavigateNetflix_Click(object sender, RoutedEventArgs e)
    {
        webView.Source = new Uri("https://www.netflix.com");
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
        webView.Source = new Uri("https://www.youtube.com");
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
            MaximizeButton.Content = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "🗗";
        }
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsBrowserWindowOpen = false;
        }
        Hide();
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            e.Cancel = true;
            if (_viewModel != null)
            {
                _viewModel.IsBrowserWindowOpen = false;
            }
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isWindowedFullscreen)
        {
            e.Handled = true;
            ApplyWindowedFullscreen(false);
        }

        base.OnPreviewKeyDown(e);
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
            _settingsService.CurrentSettings.BrowserLeft = Left;
            _settingsService.CurrentSettings.BrowserTop = Top;
            _settingsService.CurrentSettings.BrowserWidth = Width;
            _settingsService.CurrentSettings.BrowserHeight = Height;
            _settingsService.Save();
        }
    }
}
