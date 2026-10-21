using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace NotchMusic.Views;

public partial class YouTubeMusicWindow : Window
{
    private const string YouTubeMusicUrl = "https://music.youtube.com/";
    private bool _isForceClose;
    private bool _isInitialized;

    public YouTubeMusicWindow()
    {
        InitializeComponent();
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

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

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
                    StatusText.Text = "Tersambung ke DRANOTCH";
                    BackButton.IsEnabled = webView.CanGoBack;
                    ForwardButton.IsEnabled = webView.CanGoForward;
                });
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
