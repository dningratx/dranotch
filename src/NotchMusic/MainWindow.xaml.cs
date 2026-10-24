using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Tray;
using NotchMusic.ViewModels;
using NotchMusic.Views;

namespace NotchMusic;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly ITrayIconService _trayService;
    private YouTubeMusicWindow? _ytMusicWindow;
    private FloatingBrowserWindow? _floatingBrowserWindow;
    private RadioWindow? _radioWindow;
    private TodoWindow? _todoWindow;
    private PrayerWindow? _prayerWindow;
    private HotkeyManager? _hotkeyManager;
    private bool _isRealExit;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        var lyricsService = new LyricsService(_settingsService);
        var mediaService = new WindowsMediaSessionService();
        _trayService = new TrayIconService();

        _viewModel = new MainViewModel(mediaService, lyricsService, _settingsService);
        DataContext = _viewModel;

        _viewModel.RequestExpand += () => _viewModel.CurrentMode = PanelMode.Expanded;
        _viewModel.RequestCollapse += () => _viewModel.CurrentMode = PanelMode.Compact;
        _viewModel.RequestOpenSettings += OpenSettings;
        _viewModel.RequestOpenYouTubeMusic += OpenYouTubeMusic;
        _viewModel.RequestOpenFloatingBrowser += OpenFloatingBrowser;
        _viewModel.RequestOpenRadio += OpenRadio;
        _viewModel.RequestExit += () => Dispatcher.Invoke(ExitApplication);
        _viewModel.RequestComponentLayoutChanged += () => Dispatcher.Invoke(() =>
        {
            UpdateTodoWindowState();
            UpdatePrayerWindowState();
            UpdateBrowserWindowState();
            UpdateRadioWindowState();
        });
        _viewModel.RequestShowTrayNotification += (title, msg) => Dispatcher.Invoke(() => _trayService.ShowNotification(title, msg));

        _viewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.CurrentMode) ||
                ev.PropertyName == nameof(MainViewModel.IsCompactMode) ||
                ev.PropertyName == nameof(MainViewModel.IsIdleMiniMode) ||
                ev.PropertyName == nameof(MainViewModel.ShowIdleMiniView))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WindowHelper.CenterAtTopBezel(this);
                    _settingsService.CurrentSettings.PanelLeft = Left;
                    _settingsService.CurrentSettings.PanelTop = Top;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        SizeChanged += (s, ev) =>
        {
            if (ev.NewSize.Width > 0)
            {
                if (_viewModel.IsCompactMode || _viewModel.IsLyricsMode)
                {
                    WindowHelper.CenterAtTopBezel(this);
                }
                else if (ev.PreviousSize.Width > 0 && Math.Abs(ev.PreviousSize.Width - ev.NewSize.Width) > 1)
                {
                    double center = Left + ev.PreviousSize.Width / 2.0;
                    Left = center - ev.NewSize.Width / 2.0;
                    WindowHelper.DockToTopBezel(this);
                }
            }
        };

        SetupTray();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        // Position window top-center flush with the top bezel of Windows
        var settings = _settingsService.CurrentSettings;
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        if (!_viewModel.IsCompactMode && settings.PanelLeft > 0)
        {
            Left = settings.PanelLeft;
            Top = 0;
            WindowHelper.DockToTopBezel(this);
        }
        else
        {
            if (primary != null)
            {
                var bounds = primary.Bounds;
                double screenLeft = bounds.Left / dpiX;
                double screenTop = bounds.Top / dpiY;
                double screenWidth = bounds.Width / dpiX;
                Left = screenLeft + (screenWidth - ActualWidth) / 2.0;
                Top = screenTop;
            }
            WindowHelper.CenterAtTopBezel(this);
        }

        _settingsService.CurrentSettings.PanelLeft = Left;
        _settingsService.CurrentSettings.PanelTop = Top;

        // Setup Hotkeys
        SetupHotkeys();

        // Listen for screen resolution / monitor change
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, ev) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_viewModel.IsCompactMode)
                    WindowHelper.CenterAtTopBezel(this);
                else
                    WindowHelper.DockToTopBezel(this);
            });
        };

        UpdateTodoWindowState();
        UpdatePrayerWindowState();

        await _viewModel.InitializeAsync();
    }

    private void SetupTray()
    {
        _trayService.Initialize();
        _trayService.ToggleVisibilityRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Hide();
                    _todoWindow?.Hide();
                    _prayerWindow?.Hide();
                }
                else
                {
                    Show();
                    Activate();
                    if (_viewModel.IsCompactMode)
                        WindowHelper.CenterAtTopBezel(this);
                    else
                        WindowHelper.DockToTopBezel(this);
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
                    }
                    if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes)
                    {
                        _prayerWindow?.Show();
                    }
                }
            });
        };

        _trayService.ModeChangeRequested += (mode) =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                _viewModel.CurrentMode = mode;
                if (_viewModel.IsCompactMode)
                    WindowHelper.CenterAtTopBezel(this);
                else
                    WindowHelper.DockToTopBezel(this);
            });
        };

        _trayService.ToggleClickThroughRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _viewModel.IsClickThrough = !_viewModel.IsClickThrough;
                WindowHelper.SetClickThrough(this, _viewModel.IsClickThrough);
            });
        };

        _trayService.OpenYouTubeMusicRequested += () => Dispatcher.Invoke(OpenYouTubeMusic);
        _trayService.OpenRadioRequested += () => Dispatcher.Invoke(OpenRadio);
        _trayService.OpenFloatingBrowserRequested += () => Dispatcher.Invoke(OpenFloatingBrowser);
        _trayService.OpenSettingsRequested += () => Dispatcher.Invoke(OpenSettings);

        _trayService.ExitRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _isRealExit = true;
                Close();
            });
        };
    }

    private void SetupHotkeys()
    {
        _hotkeyManager?.Dispose();
        _hotkeyManager = new HotkeyManager(this);

        var settings = _settingsService.CurrentSettings;
        if (!string.IsNullOrEmpty(settings.ToggleVisibilityHotkey))
        {
            _hotkeyManager.Register(settings.ToggleVisibilityHotkey, () =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Hide();
                    _todoWindow?.Hide();
                    _prayerWindow?.Hide();
                }
                else
                {
                    Show();
                    Activate();
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
                    }
                    if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes)
                    {
                        _prayerWindow?.Show();
                    }
                }
            }, out _);
        }

        if (!string.IsNullOrEmpty(settings.ToggleLyricsHotkey))
        {
            _hotkeyManager.Register(settings.ToggleLyricsHotkey, () =>
            {
                Show();
                _viewModel.CurrentMode = _viewModel.CurrentMode == PanelMode.Lyrics ? PanelMode.Compact : PanelMode.Lyrics;
            }, out _);
        }
    }

    private void UpdateTodoWindowState()
    {
        if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection && _viewModel.IsTodoWindowOpen)
        {
            if (_todoWindow == null)
            {
                _todoWindow = new TodoWindow(_viewModel, _settingsService);
                _todoWindow.Closed += (s, e) => _todoWindow = null;
            }
            if (!_todoWindow.IsVisible)
            {
                _todoWindow.Show();
            }
        }
        else
        {
            _todoWindow?.Hide();
        }
    }

    private void UpdatePrayerWindowState()
    {
        if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes && _viewModel.IsPrayerWindowOpen)
        {
            if (_prayerWindow == null)
            {
                _prayerWindow = new PrayerWindow(_viewModel, _settingsService);
                _prayerWindow.Closed += (s, e) => _prayerWindow = null;
            }
            if (!_prayerWindow.IsVisible)
            {
                _prayerWindow.Show();
            }
        }
        else
        {
            _prayerWindow?.Hide();
        }
    }

    private void UpdateBrowserWindowState()
    {
        if (_viewModel.IsBrowserWindowOpen)
        {
            OpenFloatingBrowser();
        }
        else
        {
            _floatingBrowserWindow?.Hide();
        }
    }

    private void UpdateRadioWindowState()
    {
        if (_viewModel.IsRadioWindowOpen)
        {
            OpenRadio();
        }
        else
        {
            _radioWindow?.Hide();
        }
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_settingsService, _ => SetupHotkeys(), _viewModel.GoogleDriveSyncService, _viewModel.UpdateService, _viewModel.PrayerService);
        if (_viewModel.HasAvailableUpdate && _viewModel.AvailableUpdateInfo != null)
        {
            settingsVm.AvailableUpdate = _viewModel.AvailableUpdateInfo;
            settingsVm.HasAvailableUpdate = true;
            settingsVm.UpdateStatus = $"Tersedia versi baru: v{_viewModel.AvailableUpdateInfo.Version} ({_viewModel.AvailableUpdateInfo.UpdateTypeLabel})";
        }
        var win = new SettingsWindow(settingsVm) { Owner = this };
        win.ShowDialog();
        _viewModel.RefreshComponentLayout();
        UpdateTodoWindowState();
        UpdatePrayerWindowState();
        UpdateBrowserWindowState();
        UpdateRadioWindowState();
    }

    public void OpenYouTubeMusic()
    {
        try
        {
            // Minimize Dranotch ke mode compact saat YT Music dibuka
            _viewModel.CurrentMode = PanelMode.Compact;

            if (_ytMusicWindow == null)
            {
                _ytMusicWindow = new YouTubeMusicWindow();
                _ytMusicWindow.Closed += (s, e) => _ytMusicWindow = null;
            }

            // Dapatkan area monitor tempat Dranotch berada
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
            var workingArea = screen.WorkingArea;

            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double areaLeft = workingArea.Left / dpiX;
            double areaTop = workingArea.Top / dpiY;
            double areaWidth = workingArea.Width / dpiX;
            double areaHeight = workingArea.Height / dpiY;

            // Ukuran ideal tidak melebihi batas layar Windows
            double horizontalMargin = 20;
            double verticalMargin = 16;
            double targetWidth = Math.Min(1080, areaWidth - (horizontalMargin * 2));
            if (targetWidth < _ytMusicWindow.MinWidth)
            {
                targetWidth = Math.Min(_ytMusicWindow.MinWidth, areaWidth - 10);
            }

            // Terbuka di bawah dranotch dan tengah horizontalnya
            double notchHeight = 64;
            double notchBottom = Top + notchHeight;
            if (notchBottom < areaTop) notchBottom = areaTop;

            double targetTop = notchBottom + 8;
            double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
            double targetHeight = Math.Min(720, availableHeight);

            if (targetHeight < _ytMusicWindow.MinHeight)
            {
                targetHeight = Math.Min(_ytMusicWindow.MinHeight, areaHeight - 30);
                targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
            }

            double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

            _ytMusicWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            _ytMusicWindow.Left = targetLeft;
            _ytMusicWindow.Top = targetTop;
            _ytMusicWindow.Width = targetWidth;
            _ytMusicWindow.Height = targetHeight;

            if (_ytMusicWindow.WindowState == WindowState.Minimized)
            {
                _ytMusicWindow.WindowState = WindowState.Normal;
            }

            _ytMusicWindow.Show();
            _ytMusicWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar YouTube Music:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenFloatingBrowser()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsBrowserWindowOpen = true;

            if (_floatingBrowserWindow == null)
            {
                _floatingBrowserWindow = new FloatingBrowserWindow(_viewModel, _settingsService);
                _floatingBrowserWindow.Closed += (s, e) => _floatingBrowserWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.BrowserLeft > 0 && saved.BrowserTop >= 0 && saved.BrowserWidth >= _floatingBrowserWindow.MinWidth && saved.BrowserHeight >= _floatingBrowserWindow.MinHeight)
                {
                    _floatingBrowserWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _floatingBrowserWindow.Left = saved.BrowserLeft;
                    _floatingBrowserWindow.Top = saved.BrowserTop;
                    _floatingBrowserWindow.Width = saved.BrowserWidth;
                    _floatingBrowserWindow.Height = saved.BrowserHeight;
                }
                else
                {
                    var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                    var workingArea = screen.WorkingArea;

                    var source = PresentationSource.FromVisual(this);
                    double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                    double areaLeft = workingArea.Left / dpiX;
                    double areaTop = workingArea.Top / dpiY;
                    double areaWidth = workingArea.Width / dpiX;
                    double areaHeight = workingArea.Height / dpiY;

                    double horizontalMargin = 20;
                    double verticalMargin = 16;
                    double targetWidth = Math.Min(960, areaWidth - (horizontalMargin * 2));
                    if (targetWidth < _floatingBrowserWindow.MinWidth)
                    {
                        targetWidth = Math.Min(_floatingBrowserWindow.MinWidth, areaWidth - 10);
                    }

                    double notchHeight = 90;
                    double notchBottom = Top + notchHeight;
                    if (notchBottom < areaTop) notchBottom = areaTop;

                    double targetTop = notchBottom + 8;
                    double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
                    double targetHeight = Math.Min(540, availableHeight);

                    if (targetHeight < _floatingBrowserWindow.MinHeight)
                    {
                        targetHeight = Math.Min(_floatingBrowserWindow.MinHeight, areaHeight - 30);
                        targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
                    }

                    double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

                    _floatingBrowserWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _floatingBrowserWindow.Left = targetLeft;
                    _floatingBrowserWindow.Top = targetTop;
                    _floatingBrowserWindow.Width = targetWidth;
                    _floatingBrowserWindow.Height = targetHeight;
                }
            }

            if (_floatingBrowserWindow.WindowState == WindowState.Minimized)
            {
                _floatingBrowserWindow.WindowState = WindowState.Normal;
            }

            _floatingBrowserWindow.Show();
            _floatingBrowserWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka browser mengambang:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenRadio()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsRadioWindowOpen = true;

            if (_radioWindow == null)
            {
                _radioWindow = new RadioWindow(_viewModel, _settingsService);
                _radioWindow.Closed += (s, e) => _radioWindow = null;
            }

            if (_radioWindow.WindowState == WindowState.Minimized)
            {
                _radioWindow.WindowState = WindowState.Normal;
            }

            _radioWindow.Show();
            _radioWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Radio Online:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Island_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            if (_viewModel.IsCompactMode)
            {
                WindowHelper.CenterAtTopBezel(this);
            }
            else
            {
                WindowHelper.DockToTopBezel(this);
            }
            _settingsService.CurrentSettings.PanelLeft = Left;
            _settingsService.CurrentSettings.PanelTop = Top;
            _settingsService.Save();
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseEnter();
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseLeave();
    }

    private void ProgressBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
    }

    private void ProgressBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            _viewModel.SeekCommand.Execute(slider.Value);
        }
    }

    private void ToggleClickThrough_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsClickThrough = !_viewModel.IsClickThrough;
        WindowHelper.SetClickThrough(this, _viewModel.IsClickThrough);
    }

    public void ExitApplication()
    {
        _isRealExit = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isRealExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _ytMusicWindow?.ForceClose();
        _floatingBrowserWindow?.ForceClose();
        _radioWindow?.ForceClose();
        _viewModel.RadioService.Dispose();
        _todoWindow?.ForceClose();
        _prayerWindow?.Close();
        _hotkeyManager?.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}