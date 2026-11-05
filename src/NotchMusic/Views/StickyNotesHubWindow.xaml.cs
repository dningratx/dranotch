using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class StickyNotesHubWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isRealClose;

    public StickyNotesHubWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += StickyNotesHubWindow_Loaded;
        LocationChanged += StickyNotesHubWindow_LocationChanged;
        SizeChanged += StickyNotesHubWindow_SizeChanged;
    }

    private void StickyNotesHubWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.StickyNotesHubLeft > 0 && s.StickyNotesHubTop > 0)
        {
            Left = s.StickyNotesHubLeft;
            Top = s.StickyNotesHubTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                Left = Math.Max(workArea.Left + 40, (workArea.Width - Width) / 2);
                Top = Math.Max(workArea.Top + 60, (workArea.Height - Height) / 2);
            }
        }

        if (s.StickyNotesHubWidth >= MinWidth) Width = s.StickyNotesHubWidth;
        if (s.StickyNotesHubHeight >= MinHeight) Height = s.StickyNotesHubHeight;

        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void StickyNotesHubWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.StickyNotesHubLeft = Left;
            _settingsService.CurrentSettings.StickyNotesHubTop = Top;
            _settingsService.Save();
        }
    }

    private void StickyNotesHubWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.StickyNotesHubWidth = ActualWidth;
            _settingsService.CurrentSettings.StickyNotesHubHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsStickyNotesHubOpen = false;
        Hide();
    }

    #region 8-Direction Resizing (Thumb Handlers)

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.StickyNotesHubWidth = ActualWidth;
            _settingsService.CurrentSettings.StickyNotesHubHeight = ActualHeight;
            _settingsService.CurrentSettings.StickyNotesHubLeft = Left;
            _settingsService.CurrentSettings.StickyNotesHubTop = Top;
            _settingsService.Save();
        }
    }

    private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double current = double.IsNaN(Width) ? ActualWidth : Width;
        double newWidth = current + e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }
    }

    private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double current = double.IsNaN(Height) ? ActualHeight : Height;
        double newHeight = current + e.VerticalChange;
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
        }
    }

    private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentW = double.IsNaN(Width) ? ActualWidth : Width;
        double currentH = double.IsNaN(Height) ? ActualHeight : Height;
        double newWidth = currentW + e.HorizontalChange;
        double newHeight = currentH + e.VerticalChange;

        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
        }
    }

    private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double current = double.IsNaN(Width) ? ActualWidth : Width;
        double newWidth = current - e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Left += e.HorizontalChange;
            Width = newWidth;
        }
    }

    private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentW = double.IsNaN(Width) ? ActualWidth : Width;
        double currentH = double.IsNaN(Height) ? ActualHeight : Height;
        double newWidth = currentW - e.HorizontalChange;
        double newHeight = currentH + e.VerticalChange;

        if (newWidth >= MinWidth)
        {
            Left += e.HorizontalChange;
            Width = newWidth;
        }
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
        }
    }

    private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double current = double.IsNaN(Height) ? ActualHeight : Height;
        double newHeight = current - e.VerticalChange;
        if (newHeight >= MinHeight)
        {
            Top += e.VerticalChange;
            Height = newHeight;
        }
    }

    private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentW = double.IsNaN(Width) ? ActualWidth : Width;
        double currentH = double.IsNaN(Height) ? ActualHeight : Height;
        double newWidth = currentW - e.HorizontalChange;
        double newHeight = currentH - e.VerticalChange;

        if (newWidth >= MinWidth)
        {
            Left += e.HorizontalChange;
            Width = newWidth;
        }
        if (newHeight >= MinHeight)
        {
            Top += e.VerticalChange;
            Height = newHeight;
        }
    }

    private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentW = double.IsNaN(Width) ? ActualWidth : Width;
        double currentH = double.IsNaN(Height) ? ActualHeight : Height;
        double newWidth = currentW + e.HorizontalChange;
        double newHeight = currentH - e.VerticalChange;

        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }
        if (newHeight >= MinHeight)
        {
            Top += e.VerticalChange;
            Height = newHeight;
        }
    }

    #endregion

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
            _viewModel.IsStickyNotesHubOpen = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
