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
    private TodoWindow? _todoWindow;
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
        _viewModel.RequestComponentLayoutChanged += () => Dispatcher.Invoke(UpdateTodoWindowState);

        SizeChanged += (s, ev) =>
        {
            if (ev.PreviousSize.Width > 0 && ev.NewSize.Width > 0 && Math.Abs(ev.PreviousSize.Width - ev.NewSize.Width) > 1)
            {
                double center = Left + ev.PreviousSize.Width / 2.0;
                Left = center - ev.NewSize.Width / 2.0;
                WindowHelper.EnsureInVisibleScreen(this);
            }
        };

        SetupTray();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        // Position window top-center by default or restore saved position
        var settings = _settingsService.CurrentSettings;
        if (settings.PanelLeft > 0 && settings.PanelTop > 0)
        {
            Left = settings.PanelLeft;
            Top = settings.PanelTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                Left = workArea.Left + (workArea.Width - ActualWidth) / 2.0;
                Top = workArea.Top + 14;
            }
        }

        WindowHelper.EnsureInVisibleScreen(this);

        // Setup Hotkeys
        SetupHotkeys();

        // Listen for screen resolution / monitor change
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, ev) =>
        {
            Dispatcher.Invoke(() => WindowHelper.EnsureInVisibleScreen(this));
        };

        UpdateTodoWindowState();

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
                }
                else
                {
                    Show();
                    Activate();
                    WindowHelper.EnsureInVisibleScreen(this);
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
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
                WindowHelper.EnsureInVisibleScreen(this);
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
                }
                else
                {
                    Show();
                    Activate();
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
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
        if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
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

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_settingsService, _ => SetupHotkeys(), _viewModel.GoogleDriveSyncService, _viewModel.UpdateService);
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
    }

    public void OpenYouTubeMusic()
    {
        try
        {
            if (_ytMusicWindow == null)
            {
                _ytMusicWindow = new YouTubeMusicWindow();
                _ytMusicWindow.Closed += (s, e) => _ytMusicWindow = null;
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

    private void Island_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
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

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isRealExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _ytMusicWindow?.ForceClose();
        _todoWindow?.ForceClose();
        _hotkeyManager?.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}