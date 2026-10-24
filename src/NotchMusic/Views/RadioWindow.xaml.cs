using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class RadioWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isForceClose;

    public RadioWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += RadioWindow_Loaded;
        LocationChanged += RadioWindow_LocationChanged;
        SizeChanged += RadioWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsRadioWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsRadioWindowCompact));
            }
        };
    }

    private void RadioWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.RadioWindowLeft > 0 && s.RadioWindowTop > 0)
        {
            Left = s.RadioWindowLeft;
            Top = s.RadioWindowTop;
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

        UpdateCompactMode(_viewModel.IsRadioWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        if (isCompact)
        {
            MinWidth = 280;
            MinHeight = 0;
            Width = 360;
        }
        else
        {
            MinWidth = 340;
            MinHeight = 0;
            Width = 410;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void RadioWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.RadioWindowLeft = Left;
            _settingsService.CurrentSettings.RadioWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void RadioWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsRadioWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth)
        {
            _settingsService.CurrentSettings.RadioWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.RadioWindowHeight = ActualHeight;
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
        _viewModel.IsRadioWindowOpen = false;
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
            _viewModel.IsRadioWindowOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
