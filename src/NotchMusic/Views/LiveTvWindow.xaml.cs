using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.LiveTv;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace NotchMusic.Views;

public partial class LiveTvWindow : Window
{
    private const string VirtualHostName = "livetv.dranotch.local";
    private const string PlayerPageUrl = "https://livetv.dranotch.local/player.html";

    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly ILiveTvService _liveTvService;

    private bool _isForceClose;
    private bool _isWebViewReady;
    private bool _isCinemaMode;
    private string? _pendingStreamUrl;

    public LiveTvWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        _liveTvService = viewModel.LiveTvService;
        DataContext = _viewModel;

        Topmost = _settingsService.CurrentSettings.LiveTvAlwaysOnTop;
        UpdatePinVisuals();

        Loaded += LiveTvWindow_Loaded;
        LocationChanged += LiveTvWindow_LocationChanged;
        SizeChanged += LiveTvWindow_SizeChanged;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _liveTvService.PlayStreamRequested += OnPlayStreamRequested;
        _liveTvService.TogglePlayPauseRequested += OnTogglePlayPauseRequested;
        _liveTvService.StopStreamRequested += OnStopStreamRequested;
        _liveTvService.VolumeChanged += OnVolumeChanged;
    }

    private async void LiveTvWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.LiveTvWindowLeft > 0 && s.LiveTvWindowTop >= 0)
        {
            Left = s.LiveTvWindowLeft;
            Top = s.LiveTvWindowTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double areaLeft = workArea.Left / dpiX;
                double areaTop = workArea.Top / dpiY;
                double areaWidth = workArea.Width / dpiX;

                Left = areaLeft + (areaWidth - Width) / 2.0;
                Top = areaTop + 68;
            }
        }

        UpdateWindowLayoutMode(_viewModel.IsLiveTvWindowCompact, _isCinemaMode);
        WindowHelper.EnsureInVisibleScreen(this);

        await InitializePlayerWebViewAsync();

        // Automatically load Indonesian channels from Famelack in the background
        _ = _viewModel.EnsureInitialLiveTvCatalogLoadedAsync();
    }

    private async Task InitializePlayerWebViewAsync()
    {
        if (_isWebViewReady) return;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataFolder = Path.Combine(localAppData, "DRANOTCH", "LiveTvProfile");
            var playerAssetsFolder = Path.Combine(localAppData, "DRANOTCH", "LiveTvPlayer");
            Directory.CreateDirectory(userDataFolder);
            Directory.CreateDirectory(playerAssetsFolder);

            var playerHtmlPath = Path.Combine(playerAssetsFolder, "player.html");
            await File.WriteAllTextAsync(playerHtmlPath, GetEmbeddedPlayerHtml());

            var options = new CoreWebView2EnvironmentOptions(
                "--autoplay-policy=no-user-gesture-required --disable-web-security --allow-running-insecure-content");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: options);
            await tvWebView.EnsureCoreWebView2Async(env);

            tvWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            tvWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            tvWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

            tvWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHostName,
                playerAssetsFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            tvWebView.CoreWebView2.NewWindowRequested += (s, ev) =>
            {
                ev.Handled = true;
            };

            tvWebView.CoreWebView2.ContainsFullScreenElementChanged += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    bool isFs = tvWebView.CoreWebView2.ContainsFullScreenElement;
                    if (isFs != _isCinemaMode && !_viewModel.IsLiveTvWindowCompact)
                    {
                        _isCinemaMode = isFs;
                        UpdateWindowLayoutMode(_viewModel.IsLiveTvWindowCompact, _isCinemaMode);
                    }
                });
            };

            tvWebView.CoreWebView2.WebMessageReceived += (s, ev) =>
            {
                try
                {
                    var raw = ev.TryGetWebMessageAsString();
                    if (string.IsNullOrWhiteSpace(raw)) return;

                    using var doc = JsonDocument.Parse(raw);
                    var type = doc.RootElement.TryGetProperty("type", out var tProp) ? tProp.GetString() : null;
                    switch (type)
                    {
                        case "playing":
                            _liveTvService.NotifyPlaybackPlaying();
                            break;
                        case "paused":
                            _liveTvService.NotifyPlaybackPaused();
                            break;
                        case "buffering":
                            _liveTvService.NotifyPlaybackBuffering();
                            break;
                        case "error":
                            var reason = doc.RootElement.TryGetProperty("reason", out var rProp) ? rProp.GetString() : null;
                            _liveTvService.NotifyPlaybackError(reason);
                            break;
                    }
                }
                catch { }
            };

            tvWebView.NavigationCompleted += async (s, ev) =>
            {
                _isWebViewReady = true;
                Dispatcher.Invoke(() =>
                {
                    PlayerLoadingOverlay.Visibility = Visibility.Collapsed;
                    tvWebView.Visibility = Visibility.Visible;
                });

                await SyncVolumeToWebViewAsync(_liveTvService.Volume, _liveTvService.IsMuted);

                if (!string.IsNullOrWhiteSpace(_pendingStreamUrl))
                {
                    var urlToPlay = _pendingStreamUrl;
                    _pendingStreamUrl = null;
                    await LoadStreamInWebViewAsync(urlToPlay);
                }
                else if (_liveTvService.CurrentChannel != null && _liveTvService.PlaybackState == LiveTvPlaybackState.Playing)
                {
                    await LoadStreamInWebViewAsync(_liveTvService.CurrentChannel.ActiveStreamUrl);
                }
            };

            tvWebView.Source = new Uri(PlayerPageUrl);
        }
        catch (Exception ex)
        {
            PlayerLoadingOverlay.Visibility = Visibility.Collapsed;
            System.Windows.MessageBox.Show(
                $"Gagal menyiapkan pemutar video Live TV (WebView2):\n{ex.Message}",
                "DRANOTCH - Live TV Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnPlayStreamRequested(LiveTvChannel channel, string streamUrl)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (!_isWebViewReady || tvWebView.CoreWebView2 == null)
            {
                _pendingStreamUrl = streamUrl;
                return;
            }

            await LoadStreamInWebViewAsync(streamUrl);
        });
    }

    private void OnTogglePlayPauseRequested()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (!_isWebViewReady || tvWebView.CoreWebView2 == null) return;
            try
            {
                await tvWebView.CoreWebView2.ExecuteScriptAsync("window.togglePlayPause && window.togglePlayPause();");
            }
            catch { }
        });
    }

    private void OnStopStreamRequested()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            _pendingStreamUrl = null;
            if (!_isWebViewReady || tvWebView.CoreWebView2 == null) return;
            try
            {
                await tvWebView.CoreWebView2.ExecuteScriptAsync("window.stopStream && window.stopStream();");
            }
            catch { }
        });
    }

    private void OnVolumeChanged(double volume, bool isMuted)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await SyncVolumeToWebViewAsync(volume, isMuted);
        });
    }

    private async Task LoadStreamInWebViewAsync(string streamUrl)
    {
        if (!_isWebViewReady || tvWebView.CoreWebView2 == null)
        {
            _pendingStreamUrl = streamUrl;
            return;
        }

        try
        {
            var escapedUrl = JsonSerializer.Serialize(streamUrl);
            var vol = _liveTvService.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var muted = _liveTvService.IsMuted ? "true" : "false";
            var channelName = JsonSerializer.Serialize(_liveTvService.CurrentChannel?.Name ?? "Live TV");

            await tvWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.playLiveTvStream && window.playLiveTvStream({escapedUrl}, {vol}, {muted}, {channelName});");
        }
        catch { }
    }

    private async Task SyncVolumeToWebViewAsync(double volume, bool isMuted)
    {
        if (!_isWebViewReady || tvWebView.CoreWebView2 == null) return;
        try
        {
            var vol = Math.Clamp(volume, 0.0, 1.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var muted = isMuted ? "true" : "false";
            await tvWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.setPlayerVolume && window.setPlayerVolume({vol}, {muted});");
        }
        catch { }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsLiveTvWindowCompact))
        {
            Dispatcher.Invoke(() => UpdateWindowLayoutMode(_viewModel.IsLiveTvWindowCompact, _isCinemaMode));
        }
    }

    private void UpdateWindowLayoutMode(bool isCompactPip, bool isCinema)
    {
        if (isCompactPip)
        {
            // Mode 1: Floating 16:9 Mini Picture-in-Picture (PiP)
            MinWidth = 300;
            MinHeight = 205;
            Width = 380;
            Height = 258;
            VideoPlayerRow.Height = new GridLength(1, GridUnitType.Star);
            ChannelListRow.Height = new GridLength(0);
        }
        else if (isCinema)
        {
            // Mode Bioskop (Hanya Layar TV + Transport Bar, Tanpa Daftar Saluran)
            MinWidth = 380;
            MinHeight = 290;
            VideoPlayerRow.Height = new GridLength(1, GridUnitType.Star);
            ChannelListRow.Height = new GridLength(0);
            NowPlayingControlsBorder.Visibility = Visibility.Visible;
            FilterBarGrid.Visibility = Visibility.Collapsed;
            SearchBarGrid.Visibility = Visibility.Collapsed;
            ChannelListBorder.Visibility = Visibility.Collapsed;
            FooterDock.Visibility = Visibility.Collapsed;
            if (Height > 440)
            {
                Height = 360;
            }
        }
        else
        {
            // Mode 2: Studio Lengkap (Video Player 16:9 + Filter Negara/Kategori + Daftar Saluran)
            MinWidth = 380;
            MinHeight = 520;
            VideoPlayerRow.Height = new GridLength(236);
            ChannelListRow.Height = new GridLength(1, GridUnitType.Star);
            NowPlayingControlsBorder.Visibility = Visibility.Visible;
            FilterBarGrid.Visibility = Visibility.Visible;
            SearchBarGrid.Visibility = Visibility.Visible;
            ChannelListBorder.Visibility = Visibility.Visible;
            FooterDock.Visibility = Visibility.Visible;

            var s = _settingsService.CurrentSettings;
            Width = Math.Max(460, s.LiveTvWindowWidth);
            Height = Math.Max(640, s.LiveTvWindowHeight);
        }

        UpdateCinemaButtonVisuals();
        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void PinTopmostButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settingsService.CurrentSettings.LiveTvAlwaysOnTop = Topmost;
        _settingsService.Save();
        UpdatePinVisuals();
    }

    private void UpdatePinVisuals()
    {
        if (PinTopmostButton == null) return;
        if (Topmost)
        {
            PinTopmostButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A2B"));
            PinTopmostButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30D158"));
            PinTopmostButton.ToolTip = "Selalu di Atas AKTIF (Klik untuk menonaktifkan)";
        }
        else
        {
            PinTopmostButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
            PinTopmostButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#303042"));
            PinTopmostButton.ToolTip = "Selalu di Atas NONAKTIF (Klik untuk mengaktifkan)";
        }
    }

    private void CinemaModeButton_Click(object sender, RoutedEventArgs e)
    {
        _isCinemaMode = !_isCinemaMode;
        UpdateWindowLayoutMode(_viewModel.IsLiveTvWindowCompact, _isCinemaMode);
    }

    private void UpdateCinemaButtonVisuals()
    {
        if (CinemaModeButton == null) return;
        if (_isCinemaMode)
        {
            CinemaModeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#283850"));
            CinemaModeButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5882C8"));
            CinemaModeButton.ToolTip = "Mode Bioskop Aktif (Klik untuk menampilkan kembali daftar saluran TV)";
        }
        else
        {
            CinemaModeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
            CinemaModeButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#303042"));
            CinemaModeButton.ToolTip = "Mode Bioskop (Sembunyikan daftar saluran agar layar TV lebih fokus)";
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                if (e.ClickCount == 2)
                {
                    _viewModel.IsLiveTvWindowCompact = !_viewModel.IsLiveTvWindowCompact;
                }
                else
                {
                    DragMove();
                }
            }
            catch { }
        }
    }

    private void LiveTvWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top >= 0)
        {
            _settingsService.CurrentSettings.LiveTvWindowLeft = Left;
            _settingsService.CurrentSettings.LiveTvWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void LiveTvWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsLiveTvWindowCompact && !_isCinemaMode && WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.LiveTvWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.LiveTvWindowHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _liveTvService.Stop();
        _viewModel.IsLiveTvWindowOpen = false;
        Hide();
    }

    public void ForceClose()
    {
        _isForceClose = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _liveTvService.PlayStreamRequested -= OnPlayStreamRequested;
        _liveTvService.TogglePlayPauseRequested -= OnTogglePlayPauseRequested;
        _liveTvService.StopStreamRequested -= OnStopStreamRequested;
        _liveTvService.VolumeChanged -= OnVolumeChanged;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            e.Cancel = true;
            _liveTvService.Stop();
            _viewModel.IsLiveTvWindowOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private static string GetEmbeddedPlayerHtml() => @"<!DOCTYPE html>
<html lang=""id"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1.0""/>
<meta name=""referrer"" content=""strict-origin-when-cross-origin""/>
<title>DRANOTCH Live TV Player</title>
<script src=""https://cdn.jsdelivr.net/npm/hls.js@1.5.17/dist/hls.min.js""></script>
<style>
    * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; }
    html, body {
        width: 100%; height: 100%;
        background: #050508;
        color: #E8E8F2;
        font-family: 'Segoe UI Variable Display', 'Segoe UI', sans-serif;
        overflow: hidden;
    }
    #playerRoot {
        position: relative;
        width: 100%; height: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        background: radial-gradient(circle at center, #12121D 0%, #050508 100%);
    }
    video, iframe {
        width: 100%;
        height: 100%;
        border: none;
        background: #000;
        display: none;
    }
    video {
        object-fit: contain;
    }
    #standbyOverlay {
        position: absolute;
        inset: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        text-align: center;
        padding: 18px;
        pointer-events: none;
        background: radial-gradient(circle at center, rgba(22,22,34,0.92) 0%, rgba(5,5,8,0.98) 100%);
    }
    .badge {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        background: rgba(255,255,255,0.06);
        border: 1px solid rgba(255,255,255,0.12);
        border-radius: 999px;
        padding: 5px 12px;
        font-size: 11px;
        font-weight: 600;
        color: #C8C8DC;
        margin-bottom: 10px;
    }
    .dot {
        width: 7px; height: 7px;
        border-radius: 50%;
        background: #30D158;
        box-shadow: 0 0 8px #30D158;
    }
    .title {
        font-size: 14px;
        font-weight: 700;
        color: #FFFFFF;
        margin-bottom: 4px;
    }
    .subtitle {
        font-size: 11.5px;
        color: #8888A0;
        max-width: 290px;
        line-height: 1.4;
    }
</style>
</head>
<body>
<div id=""playerRoot"">
    <video id=""hlsVideo"" controls autoplay playsinline></video>
    <iframe id=""ytFrame"" allow=""autoplay; encrypted-media; picture-in-picture; fullscreen"" allowfullscreen referrerpolicy=""strict-origin-when-cross-origin""></iframe>
    <div id=""standbyOverlay"">
        <div class=""badge""><span class=""dot"" id=""statusDot""></span><span id=""overlayBadge"">LIVE TV</span></div>
        <div class=""title"" id=""overlayTitle"">Pilih Saluran TV untuk Menonton</div>
        <div class=""subtitle"" id=""overlaySub"">Klik salah satu saluran TV Indonesia atau Dunia di bawah untuk memulai streaming langsung.</div>
    </div>
</div>

<script>
    const video = document.getElementById('hlsVideo');
    const ytFrame = document.getElementById('ytFrame');
    const overlay = document.getElementById('standbyOverlay');
    const overlayBadge = document.getElementById('overlayBadge');
    const overlayTitle = document.getElementById('overlayTitle');
    const overlaySub = document.getElementById('overlaySub');
    const statusDot = document.getElementById('statusDot');

    let hls = null;
    let connectTimer = null;
    let activeMode = 'none'; // 'hls' | 'yt' | 'none'
    let currentVol = 0.85;
    let currentMuted = false;

    function postHost(payload) {
        try {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify(payload));
            }
        } catch (e) {}
    }

    function clearConnectTimer() {
        if (connectTimer) {
            clearTimeout(connectTimer);
            connectTimer = null;
        }
    }

    function showOverlay(badge, title, sub, dotColor) {
        overlayBadge.textContent = badge || 'LIVE TV';
        overlayTitle.textContent = title || '';
        overlaySub.textContent = sub || '';
        statusDot.style.background = dotColor || '#30D158';
        statusDot.style.boxShadow = '0 0 8px ' + (dotColor || '#30D158');
        overlay.style.display = 'flex';
    }

    function hideOverlay() {
        overlay.style.display = 'none';
    }

    function cleanupPlayers() {
        clearConnectTimer();
        if (hls) {
            try { hls.destroy(); } catch (e) {}
            hls = null;
        }
        try {
            video.pause();
            video.removeAttribute('src');
            video.load();
        } catch (e) {}
        video.style.display = 'none';

        try {
            ytFrame.src = 'about:blank';
        } catch (e) {}
        ytFrame.style.display = 'none';
        activeMode = 'none';
    }

    function extractYouTubeEmbedUrl(url) {
        if (!url) return null;
        const lower = url.toLowerCase();
        if (lower.includes('youtube.com/embed/') || lower.includes('youtube-nocookie.com/embed/')) {
            const base = url.split('?')[0];
            return base + '?autoplay=1&playsinline=1&rel=0&modestbranding=1&enablejsapi=1';
        }
        if (lower.includes('youtube.com/watch')) {
            try {
                const u = new URL(url);
                const v = u.searchParams.get('v');
                if (v) return 'https://www.youtube-nocookie.com/embed/' + v + '?autoplay=1&playsinline=1&rel=0&modestbranding=1&enablejsapi=1';
            } catch (e) {}
        }
        if (lower.includes('youtu.be/')) {
            const parts = url.split('youtu.be/')[1];
            if (parts) {
                const id = parts.split(/[?&/]/)[0];
                if (id) return 'https://www.youtube-nocookie.com/embed/' + id + '?autoplay=1&playsinline=1&rel=0&modestbranding=1&enablejsapi=1';
            }
        }
        return null;
    }

    video.addEventListener('playing', () => {
        clearConnectTimer();
        hideOverlay();
        postHost({ type: 'playing' });
    });

    video.addEventListener('pause', () => {
        if (activeMode === 'hls' && !video.ended) {
            postHost({ type: 'paused' });
        }
    });

    video.addEventListener('waiting', () => {
        if (activeMode === 'hls') {
            postHost({ type: 'buffering' });
        }
    });

    video.addEventListener('error', () => {
        if (activeMode === 'hls' && !hls) {
            clearConnectTimer();
            postHost({ type: 'error', reason: 'Stream tidak dapat diputar' });
        }
    });

    window.playLiveTvStream = function(url, volume, muted, channelName) {
        cleanupPlayers();
        currentVol = typeof volume === 'number' ? volume : 0.85;
        currentMuted = !!muted;

        if (!url) {
            showOverlay('OFFLINE', channelName || 'Saluran TV', 'URL stream kosong.', '#FF4D6D');
            postHost({ type: 'error', reason: 'URL kosong' });
            return;
        }

        showOverlay('MENGHUBUNGKAN...', channelName || 'Memuat Siaran...', 'Menyambungkan ke server streaming Live TV...', '#FFAA44');
        postHost({ type: 'buffering' });

        const ytEmbed = extractYouTubeEmbedUrl(url);
        if (ytEmbed) {
            activeMode = 'yt';
            ytFrame.style.display = 'block';
            ytFrame.onload = function() {
                if (activeMode === 'yt') {
                    clearConnectTimer();
                    hideOverlay();
                    window.setPlayerVolume(currentVol, currentMuted);
                    postHost({ type: 'playing' });
                }
            };
            ytFrame.src = ytEmbed;
            connectTimer = setTimeout(() => {
                if (activeMode === 'yt') {
                    hideOverlay();
                    postHost({ type: 'playing' });
                }
            }, 4000);
            return;
        }

        activeMode = 'hls';
        video.style.display = 'block';
        video.volume = Math.max(0, Math.min(1, currentVol));
        video.muted = currentMuted;

        connectTimer = setTimeout(() => {
            if (activeMode === 'hls' && video.readyState < 2) {
                cleanupPlayers();
                showOverlay('SIBUK / OFFLINE', channelName || 'Siaran Offline', 'Server tidak merespons. Klik tombol 🔄 untuk mencoba jalur cadangan.', '#FF4D6D');
                postHost({ type: 'error', reason: 'Timeout koneksi stream' });
            }
        }, 14000);

        if (window.Hls && Hls.isSupported() && (url.toLowerCase().includes('.m3u8') || !url.toLowerCase().endsWith('.mp4'))) {
            let networkRetries = 0;
            hls = new Hls({
                enableWorker: true,
                lowLatencyMode: true,
                maxBufferLength: 30,
                manifestLoadingTimeOut: 10000,
                manifestLoadingMaxRetry: 2,
                levelLoadingTimeOut: 10000
            });
            hls.loadSource(url);
            hls.attachMedia(video);
            hls.on(Hls.Events.MANIFEST_PARSED, function() {
                video.play().catch(() => {});
            });
            hls.on(Hls.Events.ERROR, function(event, data) {
                if (data.fatal) {
                    if (data.type === Hls.ErrorTypes.NETWORK_ERROR && networkRetries < 1) {
                        networkRetries++;
                        hls.startLoad();
                    } else if (data.type === Hls.ErrorTypes.MEDIA_ERROR && networkRetries < 1) {
                        networkRetries++;
                        hls.recoverMediaError();
                    } else {
                        cleanupPlayers();
                        showOverlay('SIARAN OFFLINE', channelName || 'Siaran Tidak Tersedia', 'Siaran ini sedang offline atau dibatasi wilayah (Geo-Blocked).', '#FF4D6D');
                        postHost({ type: 'error', reason: 'Stream HLS tidak aktif' });
                    }
                }
            });
        } else {
            video.src = url;
            video.play().catch(() => {
                clearConnectTimer();
                showOverlay('SIARAN OFFLINE', channelName || 'Siaran Tidak Tersedia', 'Format video tidak didukung atau sedang offline.', '#FF4D6D');
                postHost({ type: 'error', reason: 'Gagal memutar stream' });
            });
        }
    };

    window.togglePlayPause = function() {
        if (activeMode === 'hls') {
            if (video.paused) {
                video.play().catch(() => {});
            } else {
                video.pause();
            }
        } else if (activeMode === 'yt' && ytFrame.contentWindow) {
            try {
                ytFrame.contentWindow.postMessage(JSON.stringify({ event: 'command', func: 'pauseVideo', args: [] }), '*');
            } catch (e) {}
        }
    };

    window.stopStream = function() {
        cleanupPlayers();
        showOverlay('SIAP MEMUTAR', 'Siaran Dihentikan', 'Pilih saluran TV di bawah untuk menonton kembali.', '#30D158');
    };

    window.setPlayerVolume = function(vol, muted) {
        currentVol = Math.max(0, Math.min(1, vol));
        currentMuted = !!muted;
        video.volume = currentVol;
        video.muted = currentMuted;

        if (activeMode === 'yt' && ytFrame.contentWindow) {
            try {
                const ytVol = Math.round(currentVol * 100);
                ytFrame.contentWindow.postMessage(JSON.stringify({ event: 'command', func: 'setVolume', args: [ytVol] }), '*');
                ytFrame.contentWindow.postMessage(JSON.stringify({ event: 'command', func: currentMuted ? 'mute' : 'unMute', args: [] }), '*');
            } catch (e) {}
        }
    };
</script>
</body>
</html>";
}
