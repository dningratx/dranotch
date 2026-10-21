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

        if (s.TodoWindowWidth >= MinWidth) Width = s.TodoWindowWidth;
        if (s.TodoWindowHeight >= MinHeight) Height = s.TodoWindowHeight;

        WindowHelper.EnsureInVisibleScreen(this);
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
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
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
