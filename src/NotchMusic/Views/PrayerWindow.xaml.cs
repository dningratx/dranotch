using System;
using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class PrayerWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public PrayerWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += PrayerWindow_Loaded;
        LocationChanged += PrayerWindow_LocationChanged;
        SizeChanged += PrayerWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsPrayerWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsPrayerWindowCompact));
            }
        };
    }

    private void PrayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.PrayerWindowLeft > 0 && s.PrayerWindowTop > 0)
        {
            Left = s.PrayerWindowLeft;
            Top = s.PrayerWindowTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                Left = Math.Max(workArea.Left + 20, workArea.Left + 60);
                Top = workArea.Top + 60;
            }
        }

        UpdateCompactMode(_viewModel.IsPrayerWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        if (isCompact)
        {
            if (ActualWidth >= 280 && ActualHeight >= 200)
            {
                _settingsService.CurrentSettings.PrayerWindowWidth = ActualWidth;
                _settingsService.CurrentSettings.PrayerWindowHeight = ActualHeight;
                _settingsService.Save();
            }

            MinWidth = 240;
            MinHeight = 0;
            Width = 320;
            Height = 72;
        }
        else
        {
            MinWidth = 300;
            MinHeight = 340;

            var s = _settingsService.CurrentSettings;
            Width = s.PrayerWindowWidth >= 300 ? s.PrayerWindowWidth : 360;
            Height = s.PrayerWindowHeight >= 340 ? s.PrayerWindowHeight : 440;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void PrayerWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.PrayerWindowLeft = Left;
            _settingsService.CurrentSettings.PrayerWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void PrayerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsPrayerWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.PrayerWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.PrayerWindowHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPrayerDetached = false;
        Hide();
    }
}
