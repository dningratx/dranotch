using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class TodoWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isRealClose;

    public TodoWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += TodoWindow_Loaded;
        LocationChanged += TodoWindow_LocationChanged;
        SizeChanged += TodoWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsTodoWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsTodoWindowCompact));
            }
        };
    }

    private void TodoWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.TodoWindowLeft > 0 && s.TodoWindowTop > 0)
        {
            Left = s.TodoWindowLeft;
            Top = s.TodoWindowTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                Left = Math.Max(workArea.Left + 20, workArea.Right - Width - 40);
                Top = workArea.Top + 60;
            }
        }

        UpdateCompactMode(_viewModel.IsTodoWindowCompact);

        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        if (isCompact)
        {
            if (ActualWidth >= 300 && ActualHeight >= 200)
            {
                _settingsService.CurrentSettings.TodoWindowWidth = ActualWidth;
                _settingsService.CurrentSettings.TodoWindowHeight = ActualHeight;
                _settingsService.Save();
            }

            MinWidth = 280;
            MinHeight = 0;
            Width = 380;
            Height = 72;
        }
        else
        {
            MinWidth = 400;
            MinHeight = 360;

            var s = _settingsService.CurrentSettings;
            Width = s.TodoWindowWidth >= 400 ? s.TodoWindowWidth : 430;
            Height = s.TodoWindowHeight >= 400 ? s.TodoWindowHeight : 520;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void TodoWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.TodoWindowLeft = Left;
            _settingsService.CurrentSettings.TodoWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void TodoWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsTodoWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.TodoWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.TodoWindowHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            _settingsService.CurrentSettings.TodoWindowLeft = Left;
            _settingsService.CurrentSettings.TodoWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsTodoWindowOpen = false;
        Hide();
    }

    public void ForceClose()
    {
        _isRealClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isRealClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
