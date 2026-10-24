using System;
using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class MusicWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isForceClose;

    public MusicWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += MusicWindow_Loaded;
        LocationChanged += MusicWindow_LocationChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsMusicWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsMusicWindowCompact));
            }
        };
    }

    private void MusicWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.MusicWindowLeft > 0 && s.MusicWindowTop > 0)
        {
            Left = s.MusicWindowLeft;
            Top = s.MusicWindowTop;
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
                Top = areaTop + 72;
            }
        }

        UpdateCompactMode(_viewModel.IsMusicWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        if (isCompact)
        {
            MinWidth = 300;
            MinHeight = 0;
            Width = 380;
        }
        else
        {
            MinWidth = 420;
            MinHeight = 0;
            Width = 456;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void MusicWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.MusicWindowLeft = Left;
            _settingsService.CurrentSettings.MusicWindowTop = Top;
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
            _viewModel.IsMusicWindowOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
