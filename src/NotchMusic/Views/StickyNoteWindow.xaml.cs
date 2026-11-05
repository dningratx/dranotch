using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class StickyNoteWindow : Window
{
    public StickyNoteItemViewModel ViewModel { get; }
    public event Action? RequestCreateNewNote;

    public StickyNoteWindow(StickyNoteItemViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += StickyNoteWindow_Loaded;
        LocationChanged += StickyNoteWindow_LocationChanged;
        SizeChanged += StickyNoteWindow_SizeChanged;

        ViewModel.CloseRequested += vm =>
        {
            Dispatcher.Invoke(() =>
            {
                vm.IsClosed = true;
                Hide();
            });
        };

        ViewModel.DeleteRequested += vm =>
        {
            Dispatcher.Invoke(Close);
        };
    }

    private void StickyNoteWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        if (ViewModel.Left > 0 && ViewModel.Top > 0)
        {
            Left = ViewModel.Left;
            Top = ViewModel.Top;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                // Position towards right side with friendly default offset
                double defaultWidth = ViewModel.Width > 0 ? ViewModel.Width : 320;
                Left = Math.Max(workArea.Left + 50, workArea.Right - defaultWidth - 60);
                Top = workArea.Top + 100;
            }
        }

        if (ViewModel.Width >= MinWidth) Width = ViewModel.Width;
        if (ViewModel.Height >= MinHeight) Height = ViewModel.Height;

        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void StickyNoteWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            ViewModel.Left = Left;
            ViewModel.Top = Top;
        }
    }

    private void StickyNoteWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            ViewModel.Width = ActualWidth;
            ViewModel.Height = ActualHeight;
        }
    }

    #region Drag & Move

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

    private void TitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && !ViewModel.IsLocked)
        {
            TitleTextBlock.Visibility = Visibility.Collapsed;
            TitleEditBox.Visibility = Visibility.Visible;
            TitleEditBox.Focus();
            TitleEditBox.SelectAll();
            e.Handled = true;
        }
        else
        {
            Header_MouseDown(sender, e);
        }
    }

    private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        TitleEditBox.Visibility = Visibility.Collapsed;
        TitleTextBlock.Visibility = Visibility.Visible;
    }

    private void TitleEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            TitleEditBox.Visibility = Visibility.Collapsed;
            TitleTextBlock.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    #endregion

    #region 8-Direction Resizing (Thumb Handlers)

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            ViewModel.Width = ActualWidth;
            ViewModel.Height = ActualHeight;
            ViewModel.Left = Left;
            ViewModel.Top = Top;
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
        double newWidth = currentW + e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }

        {
            double currentH = double.IsNaN(Height) ? ActualHeight : Height;
            double newHeight = currentH + e.VerticalChange;
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
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
        double newWidth = currentW - e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Left += e.HorizontalChange;
            Width = newWidth;
        }

        {
            double currentH = double.IsNaN(Height) ? ActualHeight : Height;
            double newHeight = currentH + e.VerticalChange;
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
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
        double newWidth = currentW - e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Left += e.HorizontalChange;
            Width = newWidth;
        }

        {
            double currentH = double.IsNaN(Height) ? ActualHeight : Height;
            double newHeight = currentH - e.VerticalChange;
            if (newHeight >= MinHeight)
            {
                Top += e.VerticalChange;
                Height = newHeight;
            }
        }
    }

    private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentW = double.IsNaN(Width) ? ActualWidth : Width;
        double newWidth = currentW + e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }

        {
            double currentH = double.IsNaN(Height) ? ActualHeight : Height;
            double newHeight = currentH - e.VerticalChange;
            if (newHeight >= MinHeight)
            {
                Top += e.VerticalChange;
                Height = newHeight;
            }
        }
    }

    #endregion

    #region Action Buttons

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        RequestCreateNewNote?.Invoke();
    }

    private void ColorPalette_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.PlacementTarget = (UIElement)sender;
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Apakah Anda yakin ingin menghapus catatan tempel ini?",
            "Hapus Catatan Tempel",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ViewModel.DeleteNoteCommand.Execute(null);
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseNoteCommand.Execute(null);
    }

    #endregion
}
