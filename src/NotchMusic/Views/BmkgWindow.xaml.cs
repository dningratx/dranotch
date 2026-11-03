using System;
using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class BmkgWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isForceClose;

    public BmkgWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += BmkgWindow_Loaded;
        LocationChanged += BmkgWindow_LocationChanged;
        SizeChanged += BmkgWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBmkgWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsBmkgWindowCompact));
            }
        };
    }

    private void BmkgWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.BmkgWindowLeft > 0 && s.BmkgWindowTop > 0)
        {
            Left = s.BmkgWindowLeft;
            Top = s.BmkgWindowTop;
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

        UpdateCompactMode(_viewModel.IsBmkgWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        if (isCompact)
        {
            MinWidth = 300;
            MinHeight = 0;
            Width = 390;
        }
        else
        {
            MinWidth = 360;
            MinHeight = 0;
            Width = 440;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void BmkgWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.BmkgWindowLeft = Left;
            _settingsService.CurrentSettings.BmkgWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void BmkgWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsBmkgWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth)
        {
            _settingsService.CurrentSettings.BmkgWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.BmkgWindowHeight = ActualHeight;
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
        _viewModel.IsBmkgWindowOpen = false;
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
            _viewModel.IsBmkgWindowOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
