using System;
using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class ReminderWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isForceClose;

    public ReminderWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += ReminderWindow_Loaded;
        LocationChanged += ReminderWindow_LocationChanged;
        SizeChanged += ReminderWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsReminderWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsReminderWindowCompact));
            }
        };
    }

    private void ReminderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.ReminderWindowLeft > 0 && s.ReminderWindowTop > 0)
        {
            Left = s.ReminderWindowLeft;
            Top = s.ReminderWindowTop;
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

        UpdateCompactMode(_viewModel.IsReminderWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        if (isCompact)
        {
            MinWidth = 280;
            MinHeight = 0;
            Width = 350;
        }
        else
        {
            MinWidth = 340;
            MinHeight = 0;
            Width = 420;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void ReminderWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.ReminderWindowLeft = Left;
            _settingsService.CurrentSettings.ReminderWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void ReminderWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsReminderWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth)
        {
            _settingsService.CurrentSettings.ReminderWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.ReminderWindowHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsReminderWindowOpen = false;
        Hide();
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            e.Cancel = true;
            _viewModel.IsReminderWindowOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
