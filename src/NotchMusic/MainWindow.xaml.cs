using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly IMediaSessionService _mediaService;
    private YouTubeMusicWindow? _ytMusicWindow;
    private SpotifyWindow? _spotifyWindow;
    private FloatingBrowserWindow? _floatingBrowserWindow;
    private RadioWindow? _radioWindow;
    private LiveTvWindow? _liveTvWindow;
    private MusicWindow? _musicWindow;
    private TodoWindow? _todoWindow;
    private PrayerWindow? _prayerWindow;
    private BmkgWindow? _bmkgWindow;
    private ReminderWindow? _reminderWindow;
    private StickyNotesHubWindow? _stickyNotesHubWindow;
    private readonly Dictionary<string, StickyNoteWindow> _stickyNoteWindows = new();
    private AboutWindow? _aboutWindow;
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
        _viewModel.RequestOpenAbout += OpenAbout;
        _viewModel.RequestOpenYouTubeMusic += OpenYouTubeMusic;
        _mediaService = mediaService;
        _viewModel.RequestOpenSpotify += OpenSpotify;
        _mediaService.PlaybackStartedOnSpotify += (s, e) => Dispatcher.Invoke(PauseYouTubeMusicPlayback);
        _mediaService.PlaybackStartedOnYouTubeMusic += (s, e) => Dispatcher.Invoke(PauseSpotifyPlayback);
        _viewModel.PlaybackStartedOnSpotify += () => Dispatcher.Invoke(PauseYouTubeMusicPlayback);
        _viewModel.PlaybackStartedOnYouTubeMusic += () => Dispatcher.Invoke(PauseSpotifyPlayback);
        _viewModel.RequestVocalRemoverChanged += OnVocalRemoverChanged;
        _viewModel.RequestOpenFloatingBrowser += OpenFloatingBrowser;
        _viewModel.RequestOpenRadio += OpenRadio;
        _viewModel.RequestOpenLiveTv += OpenLiveTv;
        _viewModel.RequestOpenMusic += OpenMusic;
        _viewModel.RequestOpenBmkg += OpenBmkg;
        _viewModel.RequestOpenReminder += OpenReminder;
        _viewModel.RequestOpenStickyNotesHub += OpenStickyNotesHub;
        _viewModel.RequestOpenStickyNote += note => Dispatcher.Invoke(() => OpenStickyNote(note));
        _viewModel.RequestCloseStickyNote += note => Dispatcher.Invoke(() => CloseStickyNote(note));
        _viewModel.RequestExit += () => Dispatcher.Invoke(ExitApplication);
        _viewModel.RequestComponentLayoutChanged += () => Dispatcher.Invoke(() =>
        {
            UpdateTodoWindowState();
            UpdatePrayerWindowState();
            UpdateBmkgWindowState();
            UpdateReminderWindowState();
            UpdateStickyNotesWindowState();
            UpdateMusicWindowState();
            UpdateBrowserWindowState();
            UpdateRadioWindowState();
            UpdateLiveTvWindowState();
        });
        _viewModel.RequestShowTrayNotification += (title, msg) => Dispatcher.Invoke(() => _trayService.ShowNotification(title, msg));
        SetupAnimations();

        _viewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.CurrentMode) ||
                ev.PropertyName == nameof(MainViewModel.IsCompactMode) ||
                ev.PropertyName == nameof(MainViewModel.IsBezelMinimized) ||
                ev.PropertyName == nameof(MainViewModel.ShowCompactView) ||
                ev.PropertyName == nameof(MainViewModel.IsNotchExpanded) ||
                ev.PropertyName == nameof(MainViewModel.ShowComponentIcons) ||
                ev.PropertyName == nameof(MainViewModel.IsIdleMiniMode) ||
                ev.PropertyName == nameof(MainViewModel.ShowIdleMiniView) ||
                ev.PropertyName == nameof(MainViewModel.IsMusicWindowOpen) ||
                ev.PropertyName == nameof(MainViewModel.ShowMusicInNotch) ||
                ev.PropertyName == nameof(MainViewModel.ShowIdlePrayerBesideLogo) ||
                ev.PropertyName == nameof(MainViewModel.ShowIdleWeatherBesideLogo) ||
                ev.PropertyName == nameof(MainViewModel.IsTodoDetachedWindowVisible))
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
        UpdateBmkgWindowState();
        UpdateReminderWindowState();
        UpdateStickyNotesWindowState();
        UpdateMusicWindowState();

        if (!_viewModel.ShowComponentIcons)
        {
            if (LeftComponentPanel != null) LeftComponentPanel.Visibility = Visibility.Collapsed;
            if (RightComponentPanel != null) RightComponentPanel.Visibility = Visibility.Collapsed;
            if (BottomComponentPanel != null) BottomComponentPanel.Visibility = Visibility.Collapsed;
        }

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
                    _bmkgWindow?.Hide();
                    _reminderWindow?.Hide();
                    _stickyNotesHubWindow?.Hide();
                    foreach (var win in _stickyNoteWindows.Values)
                    {
                        win.Hide();
                    }
                    _musicWindow?.Hide();
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
                    if (_viewModel.EnableBmkg && _viewModel.IsBmkgWindowOpen)
                    {
                        _bmkgWindow?.Show();
                    }
                    if (_viewModel.EnableReminders && _viewModel.IsReminderWindowOpen)
                    {
                        _reminderWindow?.Show();
                    }
                    if (_viewModel.EnableStickyNotes && _viewModel.IsStickyNotesHubOpen)
                    {
                        _stickyNotesHubWindow?.Show();
                    }
                    foreach (var note in _viewModel.StickyNotes)
                    {
                        if (!note.IsClosed && _stickyNoteWindows.TryGetValue(note.Id, out var win))
                        {
                            win.Show();
                        }
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
        _trayService.OpenSpotifyRequested += () => Dispatcher.Invoke(OpenSpotify);
        _trayService.OpenRadioRequested += () => Dispatcher.Invoke(OpenRadio);
        _trayService.OpenLiveTvRequested += () => Dispatcher.Invoke(OpenLiveTv);
        _trayService.OpenBmkgRequested += () => Dispatcher.Invoke(OpenBmkg);
        _trayService.OpenReminderRequested += () => Dispatcher.Invoke(OpenReminder);
        _trayService.OpenStickyNotesRequested += () => Dispatcher.Invoke(OpenStickyNotesHub);
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
                    _musicWindow?.Hide();
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
        if (_viewModel.IsTodoDetached && _viewModel.IsTodoWindowOpen)
        {
            if (_todoWindow == null)
            {
                _todoWindow = new TodoWindow(_viewModel, _settingsService);
                _todoWindow.Closed += (s, e) => _todoWindow = null;
            }
            if (!_todoWindow.IsVisible)
            {
                if (_todoWindow.WindowState == WindowState.Minimized)
                {
                    _todoWindow.WindowState = WindowState.Normal;
                }
                _todoWindow.Show();
                _todoWindow.Activate();
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

    private void UpdateMusicWindowState()
    {
        if (_viewModel.IsMusicWindowOpen)
        {
            OpenMusic();
        }
        else
        {
            _musicWindow?.Hide();
        }
    }

    public void OpenMusic()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsMusicWindowOpen = true;

            if (_musicWindow == null)
            {
                _musicWindow = new MusicWindow(_viewModel, _settingsService);
                _musicWindow.Closed += (s, e) => _musicWindow = null;
            }

            if (_musicWindow.WindowState == WindowState.Minimized)
            {
                _musicWindow.WindowState = WindowState.Normal;
            }

            _musicWindow.Show();
            _musicWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Pemutar Musik:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

    private void UpdateLiveTvWindowState()
    {
        if (_viewModel.IsLiveTvWindowOpen)
        {
            OpenLiveTv();
        }
        else
        {
            _liveTvWindow?.Hide();
        }
    }

    private void UpdateBmkgWindowState()
    {
        if (_viewModel.EnableBmkg && _viewModel.IsBmkgWindowOpen)
        {
            OpenBmkg();
        }
        else
        {
            _bmkgWindow?.Hide();
        }
    }

    public void OpenBmkg()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsBmkgWindowOpen = true;

            if (_bmkgWindow == null)
            {
                _bmkgWindow = new BmkgWindow(_viewModel, _settingsService);
                _bmkgWindow.Closed += (s, e) => _bmkgWindow = null;
            }

            if (_bmkgWindow.WindowState == WindowState.Minimized)
            {
                _bmkgWindow.WindowState = WindowState.Normal;
            }

            _bmkgWindow.Show();
            _bmkgWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Cuaca & Gempa Bumi BMKG:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateReminderWindowState()
    {
        if (_viewModel.EnableReminders && _viewModel.IsReminderWindowOpen)
        {
            OpenReminder();
        }
        else
        {
            _reminderWindow?.Hide();
        }
    }

    public void OpenReminder()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsReminderWindowOpen = true;

            if (_reminderWindow == null)
            {
                _reminderWindow = new ReminderWindow(_viewModel, _settingsService);
                _reminderWindow.Closed += (s, e) => _reminderWindow = null;
            }

            if (_reminderWindow.WindowState == WindowState.Minimized)
            {
                _reminderWindow.WindowState = WindowState.Normal;
            }

            _reminderWindow.Show();
            _reminderWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Pengingat Pintar:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateStickyNotesWindowState()
    {
        if (_viewModel.EnableStickyNotes)
        {
            if (_viewModel.IsStickyNotesHubOpen)
            {
                OpenStickyNotesHub();
            }
            else
            {
                _stickyNotesHubWindow?.Hide();
            }

            foreach (var note in _viewModel.StickyNotes)
            {
                if (!note.IsClosed)
                {
                    OpenStickyNote(note);
                }
                else
                {
                    CloseStickyNote(note);
                }
            }
        }
        else
        {
            _stickyNotesHubWindow?.Hide();
            foreach (var win in _stickyNoteWindows.Values)
            {
                win.Hide();
            }
        }
    }

    public void OpenStickyNotesHub()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsStickyNotesHubOpen = true;

            if (_stickyNotesHubWindow == null)
            {
                _stickyNotesHubWindow = new StickyNotesHubWindow(_viewModel, _settingsService);
                _stickyNotesHubWindow.Closed += (s, e) => _stickyNotesHubWindow = null;
            }

            if (_stickyNotesHubWindow.WindowState == WindowState.Minimized)
            {
                _stickyNotesHubWindow.WindowState = WindowState.Normal;
            }

            _stickyNotesHubWindow.Show();
            _stickyNotesHubWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Catatan Tempel (Sticky Notes):\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenStickyNote(StickyNoteItemViewModel note)
    {
        try
        {
            if (!_stickyNoteWindows.TryGetValue(note.Id, out var win) || win == null)
            {
                win = new StickyNoteWindow(note);
                win.RequestCreateNewNote += () => _viewModel.CreateNewStickyNote(note.Color);
                win.Closed += (s, e) => _stickyNoteWindows.Remove(note.Id);
                _stickyNoteWindows[note.Id] = win;
            }

            if (win.WindowState == WindowState.Minimized)
            {
                win.WindowState = WindowState.Normal;
            }

            win.Show();
            win.Activate();
        }
        catch { }
    }

    public void CloseStickyNote(StickyNoteItemViewModel note)
    {
        try
        {
            if (_stickyNoteWindows.TryGetValue(note.Id, out var win))
            {
                win.Hide();
            }
        }
        catch { }
    }

    public void OpenAbout()
    {
        if (_aboutWindow != null && _aboutWindow.IsVisible)
        {
            _aboutWindow.Hide();
            return;
        }

        if (_aboutWindow == null)
        {
            _aboutWindow = new AboutWindow(_viewModel);
            _aboutWindow.Closed += (s, e) => _aboutWindow = null;
        }

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
        var workingArea = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = workingArea.Left / dpiX;
        double areaTop = workingArea.Top / dpiY;
        double areaWidth = workingArea.Width / dpiX;

        double notchBottom = Math.Max(areaTop, Top + Math.Max(ActualHeight, 84));
        _aboutWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _aboutWindow.Left = areaLeft + (areaWidth - 400) / 2.0;
        _aboutWindow.Top = notchBottom + 6;
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_settingsService, _ => SetupHotkeys(), _viewModel.GoogleDriveSyncService, _viewModel.UpdateService, _viewModel.PrayerService);
        settingsVm.PropertyChanged += (s, ev) =>
        {
            _viewModel.RefreshComponentLayout();
            UpdatePrayerWindowState();
            UpdateBmkgWindowState();
            UpdateReminderWindowState();
            UpdateTodoWindowState();
            _ = _viewModel.BmkgService.RefreshAllAsync();
        };
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
        UpdateBmkgWindowState();
        UpdateReminderWindowState();
        UpdateStickyNotesWindowState();
        UpdateMusicWindowState();
        UpdateBrowserWindowState();
        UpdateRadioWindowState();
    }

    private async void OnVocalRemoverChanged(bool enabled)
    {
        try
        {
            if (_ytMusicWindow != null)
            {
                await _ytMusicWindow.SetVocalRemoverAsync(enabled);
            }

            if (enabled)
            {
                _viewModel.SessionStatus = (_ytMusicWindow != null)
                    ? "🎤 Mode Karaoke (Penghilang Vokal) Aktif"
                    : "🎤 Karaoke Aktif (Putar lagu di ▶ YT Music untuk hapus vokal)";
            }
            else
            {
                _viewModel.SessionStatus = "🎤 Mode Karaoke Dinonaktifkan";
            }
        }
        catch { }
    }

    public void OpenYouTubeMusic()
    {
        try
        {
            // Minimize Dranotch ke mode compact saat YT Music dibuka
            _viewModel.CurrentMode = PanelMode.Compact;

            if (_ytMusicWindow == null)
            {
                _ytMusicWindow = new YouTubeMusicWindow(_viewModel, _settingsService);
                _ytMusicWindow.Closed += (s, e) => _ytMusicWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.YouTubeMusicLeft > 0 && saved.YouTubeMusicTop >= 0 &&
                    saved.YouTubeMusicWidth >= _ytMusicWindow.MinWidth &&
                    saved.YouTubeMusicHeight >= _ytMusicWindow.MinHeight)
                {
                    _ytMusicWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _ytMusicWindow.Left = saved.YouTubeMusicLeft;
                    _ytMusicWindow.Top = saved.YouTubeMusicTop;
                    _ytMusicWindow.Width = saved.YouTubeMusicWidth;
                    _ytMusicWindow.Height = saved.YouTubeMusicHeight;
                }
                else
                {
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
                }
            }

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

    public void OpenSpotify()
    {
        try
        {



            _viewModel.CurrentMode = PanelMode.Compact;

            if (_spotifyWindow == null)
            {
                _spotifyWindow = new SpotifyWindow(_viewModel, _settingsService);
                _spotifyWindow.Closed += (s, e) => _spotifyWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.SpotifyLeft > 0 && saved.SpotifyTop >= 0 &&
                    saved.SpotifyWidth >= _spotifyWindow.MinWidth &&
                    saved.SpotifyHeight >= _spotifyWindow.MinHeight)
                {
                    _spotifyWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _spotifyWindow.Left = saved.SpotifyLeft;
                    _spotifyWindow.Top = saved.SpotifyTop;
                    _spotifyWindow.Width = saved.SpotifyWidth;
                    _spotifyWindow.Height = saved.SpotifyHeight;
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
                    double targetWidth = Math.Min(380, areaWidth - (horizontalMargin * 2));
                    if (targetWidth < _spotifyWindow.MinWidth)
                    {
                        targetWidth = Math.Min(_spotifyWindow.MinWidth, areaWidth - 10);
                    }

                    double notchHeight = 64;
                    double notchBottom = Top + notchHeight;
                    if (notchBottom < areaTop) notchBottom = areaTop;

                    double targetTop = notchBottom + 8;
                    double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
                    double targetHeight = Math.Min(740, availableHeight);

                    if (targetHeight < _spotifyWindow.MinHeight)
                    {
                        targetHeight = Math.Min(_spotifyWindow.MinHeight, areaHeight - 30);
                        targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
                    }

                    double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

                    _spotifyWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _spotifyWindow.Left = targetLeft;
                    _spotifyWindow.Top = targetTop;
                    _spotifyWindow.Width = targetWidth;
                    _spotifyWindow.Height = targetHeight;
                }
            }

            if (_spotifyWindow.WindowState == WindowState.Minimized)
            {
                _spotifyWindow.WindowState = WindowState.Normal;
            }

            _spotifyWindow.Show();
            _spotifyWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar Spotify:\n{ex.Message}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void PauseYouTubeMusicPlayback()
    {
        _ytMusicWindow?.PausePlaybackAsync();
    }

    public void PauseSpotifyPlayback()
    {
        _spotifyWindow?.PausePlaybackAsync();
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
    }

    public void PauseAndHideYouTubeMusic()
    {
        PauseYouTubeMusicPlayback();
        if (_ytMusicWindow != null)
        {
            _ytMusicWindow.Hide();
        }
    }

    public void PauseAndHideSpotify()
    {
        PauseSpotifyPlayback();
        if (_spotifyWindow != null)
        {
            _spotifyWindow.Hide();
        }
    }

    public void PauseSpotify()
    {
        PauseSpotifyPlayback();
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

    public void OpenLiveTv()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsLiveTvWindowOpen = true;

            if (_liveTvWindow == null)
            {
                _liveTvWindow = new LiveTvWindow(_viewModel, _settingsService);
                _liveTvWindow.Closed += (s, e) => _liveTvWindow = null;
            }

            if (_liveTvWindow.WindowState == WindowState.Minimized)
            {
                _liveTvWindow.WindowState = WindowState.Normal;
            }

            _liveTvWindow.Show();
            _liveTvWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Live TV:\n{ex.Message}",
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

    private void SetupAnimations()
    {
        _viewModel.RequestAnimateExpand += AnimateExpand;
        _viewModel.RequestAnimateCollapse += AnimateCollapse;
        _viewModel.RequestAnimateBezelMinimize += AnimateBezelMinimize;
        _viewModel.RequestAnimateBezelRestore += AnimateBezelRestore;
    }

    private void AnimateExpand()
    {
        Dispatcher.Invoke(() =>
        {
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Panel Komponen Kiri: meluncur keluar dari logo tengah & memudar masuk
            if (LeftComponentPanel != null && LeftComponentTransform != null)
            {
                LeftComponentPanel.Visibility = Visibility.Visible;
                var leftSlide = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = easeOut };
                var leftFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320)) { EasingFunction = easeOut };
                LeftComponentTransform.BeginAnimation(TranslateTransform.XProperty, leftSlide);
                LeftComponentPanel.BeginAnimation(UIElement.OpacityProperty, leftFade);
            }

            // Panel Komponen Kanan: meluncur keluar dari logo tengah & memudar masuk
            if (RightComponentPanel != null && RightComponentTransform != null)
            {
                RightComponentPanel.Visibility = Visibility.Visible;
                var rightSlide = new DoubleAnimation(-30, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = easeOut };
                var rightFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320)) { EasingFunction = easeOut };
                RightComponentTransform.BeginAnimation(TranslateTransform.XProperty, rightSlide);
                RightComponentPanel.BeginAnimation(UIElement.OpacityProperty, rightFade);
            }

            // Panel Komponen Bawah: meluncur turun dari atas & memudar masuk
            if (BottomComponentPanel != null && BottomComponentTransform != null)
            {
                BottomComponentPanel.Visibility = Visibility.Visible;
                var bottomSlide = new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = easeOut };
                var bottomFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(340)) { EasingFunction = easeOut };
                BottomComponentTransform.BeginAnimation(TranslateTransform.YProperty, bottomSlide);
                BottomComponentPanel.BeginAnimation(UIElement.OpacityProperty, bottomFade);
            }

            Dispatcher.BeginInvoke(new Action(() => WindowHelper.CenterAtTopBezel(this)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        });
    }

    private void AnimateCollapse()
    {
        Dispatcher.Invoke(() =>
        {
            var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            int pending = 3;
            Action checkDone = () =>
            {
                pending--;
                if (pending <= 0)
                {
                    if (LeftComponentPanel != null)
                    {
                        LeftComponentPanel.BeginAnimation(UIElement.OpacityProperty, null);
                        LeftComponentPanel.Visibility = Visibility.Collapsed;
                    }
                    if (RightComponentPanel != null)
                    {
                        RightComponentPanel.BeginAnimation(UIElement.OpacityProperty, null);
                        RightComponentPanel.Visibility = Visibility.Collapsed;
                    }
                    if (BottomComponentPanel != null)
                    {
                        BottomComponentPanel.BeginAnimation(UIElement.OpacityProperty, null);
                        BottomComponentPanel.Visibility = Visibility.Collapsed;
                    }

                    _viewModel.FinishCollapse();
                    Dispatcher.BeginInvoke(new Action(() => WindowHelper.CenterAtTopBezel(this)),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };

            // Panel Komponen Kiri: meluncur merapat ke tengah & memudar keluar
            if (LeftComponentPanel != null && LeftComponentTransform != null)
            {
                var leftSlide = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeIn };
                var leftFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(230)) { EasingFunction = easeIn };
                leftFade.Completed += (s, e) => checkDone();
                LeftComponentTransform.BeginAnimation(TranslateTransform.XProperty, leftSlide);
                LeftComponentPanel.BeginAnimation(UIElement.OpacityProperty, leftFade);
            }
            else { checkDone(); }

            // Panel Komponen Kanan: meluncur merapat ke tengah & memudar keluar
            if (RightComponentPanel != null && RightComponentTransform != null)
            {
                var rightSlide = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeIn };
                var rightFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(230)) { EasingFunction = easeIn };
                rightFade.Completed += (s, e) => checkDone();
                RightComponentTransform.BeginAnimation(TranslateTransform.XProperty, rightSlide);
                RightComponentPanel.BeginAnimation(UIElement.OpacityProperty, rightFade);
            }
            else { checkDone(); }

            // Panel Komponen Bawah: meluncur naik & memudar keluar
            if (BottomComponentPanel != null && BottomComponentTransform != null)
            {
                var bottomSlide = new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(230)) { EasingFunction = easeIn };
                var bottomFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(210)) { EasingFunction = easeIn };
                bottomFade.Completed += (s, e) => checkDone();
                BottomComponentTransform.BeginAnimation(TranslateTransform.YProperty, bottomSlide);
                BottomComponentPanel.BeginAnimation(UIElement.OpacityProperty, bottomFade);
            }
            else { checkDone(); }
        });
    }

    private void AnimateBezelMinimize()
    {
        Dispatcher.Invoke(() =>
        {
            if (MainIslandBorder == null || MainIslandTransform == null) return;

            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var slideUp = new DoubleAnimation(MainIslandTransform.Y, -70, TimeSpan.FromMilliseconds(320)) { EasingFunction = easeIn };
            var fadeOut = new DoubleAnimation(MainIslandBorder.Opacity, 0.0, TimeSpan.FromMilliseconds(280)) { EasingFunction = easeIn };

            slideUp.Completed += (s, e) =>
            {
                MainIslandBorder.Visibility = Visibility.Collapsed;
                WindowHelper.CenterAtTopBezel(this);
            };

            MainIslandTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            MainIslandBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            if (BezelMinimizeToggleButton != null && BezelToggleTransform != null)
            {
                var toggleSlide = new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var toggleFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
                BezelToggleTransform.BeginAnimation(TranslateTransform.YProperty, toggleSlide);
                BezelMinimizeToggleButton.BeginAnimation(UIElement.OpacityProperty, toggleFade);
            }
        });
    }

    private void AnimateBezelRestore()
    {
        Dispatcher.Invoke(() =>
        {
            if (MainIslandBorder == null || MainIslandTransform == null) return;

            MainIslandBorder.Visibility = Visibility.Visible;
            MainIslandBorder.Opacity = 0.0;
            MainIslandTransform.Y = -70;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var slideDown = new DoubleAnimation(-70, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = easeOut };
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(340)) { EasingFunction = easeOut };

            slideDown.Completed += (s, e) =>
            {
                WindowHelper.CenterAtTopBezel(this);
            };

            MainIslandTransform.BeginAnimation(TranslateTransform.YProperty, slideDown);
            MainIslandBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        });
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
        _spotifyWindow?.ForceClose();
        _floatingBrowserWindow?.ForceClose();
        _radioWindow?.ForceClose();
        _liveTvWindow?.ForceClose();
        _musicWindow?.ForceClose();
        _viewModel.RadioService.Dispose();
        _viewModel.LiveTvService.Dispose();
        _viewModel.BmkgService.Dispose();
        _viewModel.ReminderService.Dispose();
        _bmkgWindow?.ForceClose();
        _reminderWindow?.ForceClose();
        _stickyNotesHubWindow?.ForceClose();
        foreach (var win in _stickyNoteWindows.Values.ToList())
        {
            try { win.Close(); } catch { }
        }
        _todoWindow?.ForceClose();
        _prayerWindow?.Close();
        _aboutWindow?.Close();
        _hotkeyManager?.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}
