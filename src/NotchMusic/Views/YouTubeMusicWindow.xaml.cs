using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NotchMusic.Helpers;
using NotchMusic.ViewModels;
using Color = System.Windows.Media.Color;

namespace NotchMusic.Views;

public partial class YouTubeMusicWindow : Window
{
    private const string YouTubeMusicUrl = "https://music.youtube.com/";
    private readonly MainViewModel? _viewModel;
    private bool _isForceClose;
    private bool _isInitialized;
    private bool _isVocalRemoverEnabled;

    public YouTubeMusicWindow(MainViewModel? viewModel = null)
    {
        _viewModel = viewModel;
        _isVocalRemoverEnabled = _viewModel?.IsVocalRemoverEnabled ?? false;
        InitializeComponent();
        try
        {
            var uri = new Uri("pack://application:,,,/NotchMusic;component/Assets/app.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }

        UpdateVocalRemoverButtonVisuals();
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
                    StatusText.Text = _isVocalRemoverEnabled
                        ? "🎤 Mode Karaoke (Penghilang Vokal) Aktif"
                        : "Tersambung ke DRANOTCH";
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
        Dispatcher.Invoke(() =>
        {
            UpdateVocalRemoverButtonVisuals();
            StatusText.Text = _isVocalRemoverEnabled
                ? "🎤 Mode Karaoke (Penghilang Vokal) Aktif"
                : "Tersambung ke DRANOTCH";
        });

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

    private void UpdateVocalRemoverButtonVisuals()
    {
        if (VocalRemoverButton == null) return;
        if (_isVocalRemoverEnabled)
        {
            VocalRemoverButton.Content = "🎤 Karaoke: ON";
            VocalRemoverButton.Background = new SolidColorBrush(Color.FromRgb(42, 24, 62));
            VocalRemoverButton.Foreground = new SolidColorBrush(Color.FromRgb(233, 213, 255));
        }
        else
        {
            VocalRemoverButton.Content = "🎤 Karaoke (Hapus Vokal)";
            VocalRemoverButton.Background = System.Windows.Media.Brushes.Transparent;
            VocalRemoverButton.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
        }
    }

    private async void VocalRemoverButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsVocalRemoverEnabled = !_viewModel.IsVocalRemoverEnabled;
        }
        else
        {
            await SetVocalRemoverAsync(!_isVocalRemoverEnabled);
        }
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

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
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
