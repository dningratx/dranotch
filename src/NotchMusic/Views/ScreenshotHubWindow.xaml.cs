using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Screenshot;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;

namespace NotchMusic.Views;

public partial class ScreenshotHubWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly IScreenshotService _screenshotService;
    private readonly ISettingsService _settingsService;

    private readonly ObservableCollection<ScreenshotItemViewModel> _allScreenshots = new();
    private readonly ObservableCollection<ScreenshotItemViewModel> _filteredScreenshots = new();
    private ScreenshotItemViewModel? _selectedItem;

    private double _currentZoom = 1.0;
    private Color _currentDrawingColor = (Color)ColorConverter.ConvertFromString("#EF4444");
    private double _currentStrokeThickness = 3.0;
    private bool _isRealClose;

    public ScreenshotHubWindow(
        MainViewModel mainViewModel,
        IScreenshotService screenshotService,
        ISettingsService settingsService)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _screenshotService = screenshotService;
        _settingsService = settingsService;

        ScreenshotsListBox.ItemsSource = _filteredScreenshots;

        Loaded += ScreenshotHubWindow_Loaded;
        LocationChanged += ScreenshotHubWindow_LocationChanged;
        SizeChanged += ScreenshotHubWindow_SizeChanged;
        KeyDown += ScreenshotHubWindow_KeyDown;

        _screenshotService.ScreenshotsChanged += () => Dispatcher.Invoke(ReloadScreenshots);
        _mainViewModel.ScreenshotsSyncedFromCloud += _ => Dispatcher.Invoke(ReloadScreenshots);
    }

    private void ScreenshotHubWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.ScreenshotHubLeft > 0 && s.ScreenshotHubTop > 0)
        {
            Left = s.ScreenshotHubLeft;
            Top = s.ScreenshotHubTop;
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

        if (s.ScreenshotHubWidth >= MinWidth) Width = s.ScreenshotHubWidth;
        if (s.ScreenshotHubHeight >= MinHeight) Height = s.ScreenshotHubHeight;

        WindowHelper.EnsureInVisibleScreen(this);

        StoragePathText.Text = $"📁 {_screenshotService.GetScreenshotsFolder()}";

        SetupDrawingAttributes();
        ReloadScreenshots();
    }

    private void ScreenshotHubWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.ScreenshotHubLeft = Left;
            _settingsService.CurrentSettings.ScreenshotHubTop = Top;
        }
    }

    private void ScreenshotHubWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.ScreenshotHubWidth = ActualWidth;
            _settingsService.CurrentSettings.ScreenshotHubHeight = ActualHeight;
        }
    }

    private void ScreenshotHubWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CopySelectedItemToClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SaveSelectedItemAsFile();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            if (!DetailTitleBox.IsFocused && !SearchBox.IsFocused)
            {
                DeleteSelectedItem();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    public void ReloadScreenshots()
    {
        var items = _screenshotService.LoadScreenshots();
        _allScreenshots.Clear();

        foreach (var item in items)
        {
            _allScreenshots.Add(new ScreenshotItemViewModel(item));
        }

        ApplySearchFilter();

        HeaderBadgeText.Text = $"{_allScreenshots.Count} Gambar";
        _mainViewModel.ScreenshotCount = _allScreenshots.Count;

        if (_selectedItem == null || !_allScreenshots.Any(x => x.Id == _selectedItem.Id))
        {
            SelectFirstAvailable();
        }
        else
        {
            var match = _allScreenshots.FirstOrDefault(x => x.Id == _selectedItem.Id);
            if (match != null)
            {
                ScreenshotsListBox.SelectedItem = match;
            }
        }
    }

    private void ApplySearchFilter()
    {
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        _filteredScreenshots.Clear();

        foreach (var item in _allScreenshots)
        {
            if (string.IsNullOrEmpty(query) ||
                item.Title.ToLowerInvariant().Contains(query) ||
                item.FormattedDate.ToLowerInvariant().Contains(query) ||
                item.CaptureMode.ToLowerInvariant().Contains(query))
            {
                _filteredScreenshots.Add(item);
            }
        }

        EmptyGalleryPanel.Visibility = _filteredScreenshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ScreenshotsListBox.Visibility = _filteredScreenshots.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SelectFirstAvailable()
    {
        if (_filteredScreenshots.Count > 0)
        {
            ScreenshotsListBox.SelectedIndex = 0;
        }
        else
        {
            _selectedItem = null;
            DetailStudioGrid.Visibility = Visibility.Collapsed;
            NoSelectionPanel.Visibility = Visibility.Visible;
        }
    }

    private void ScreenshotsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScreenshotsListBox.SelectedItem is ScreenshotItemViewModel vm)
        {
            DisplaySelectedItem(vm);
        }
    }

    private void DisplaySelectedItem(ScreenshotItemViewModel vm)
    {
        _selectedItem = vm;
        DetailStudioGrid.Visibility = Visibility.Visible;
        NoSelectionPanel.Visibility = Visibility.Collapsed;

        DetailTitleBox.Text = vm.Title;
        DetailInfoText.Text = $"{vm.FormattedResolution} • {vm.FormattedFileSize} • {vm.FormattedDate}";

        try
        {
            if (File.Exists(vm.FilePath))
            {
                var bitmap = ScreenCaptureHelper.LoadBitmapImageWithoutLock(vm.FilePath);
                DetailImage.Source = bitmap;
                DetailInkCanvas.Width = bitmap.PixelWidth;
                DetailInkCanvas.Height = bitmap.PixelHeight;
                DetailInkCanvas.Strokes.Clear();

                ResetZoom();
            }
        }
        catch { }
    }

    #region Drawing & Markup

    private void SetupDrawingAttributes()
    {
        var drawingAttributes = new DrawingAttributes
        {
            Color = _currentDrawingColor,
            Width = _currentStrokeThickness,
            Height = _currentStrokeThickness,
            StylusTip = StylusTip.Ellipse,
            FitToCurve = true
        };
        DetailInkCanvas.DefaultDrawingAttributes = drawingAttributes;
    }

    private void BtnToolSelect_Click(object sender, RoutedEventArgs e)
    {
        DetailInkCanvas.EditingMode = InkCanvasEditingMode.None;
        BtnToolSelect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF"));
        BtnToolSelect.Foreground = Brushes.Black;
        BtnToolPen.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolPen.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
        BtnToolHighlighter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolHighlighter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
    }

    private void BtnToolPen_Click(object sender, RoutedEventArgs e)
    {
        DetailInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        var da = DetailInkCanvas.DefaultDrawingAttributes;
        da.IsHighlighter = false;
        da.Width = 3;
        da.Height = 3;
        da.Color = _currentDrawingColor;

        BtnToolPen.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF"));
        BtnToolPen.Foreground = Brushes.Black;
        BtnToolSelect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolSelect.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
        BtnToolHighlighter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolHighlighter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
    }

    private void BtnToolHighlighter_Click(object sender, RoutedEventArgs e)
    {
        DetailInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        var da = DetailInkCanvas.DefaultDrawingAttributes;
        da.IsHighlighter = true;
        da.Width = 18;
        da.Height = 18;
        da.Color = Color.FromArgb(128, 250, 204, 21); // Semi-transparent yellow

        BtnToolHighlighter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF"));
        BtnToolHighlighter.Foreground = Brushes.Black;
        BtnToolSelect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolSelect.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
        BtnToolPen.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E26"));
        BtnToolPen.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4E0"));
    }

    private void ColorSwatch_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string hex)
        {
            _currentDrawingColor = (Color)ColorConverter.ConvertFromString(hex);
            DetailInkCanvas.DefaultDrawingAttributes.Color = _currentDrawingColor;
            BtnToolPen_Click(sender, e);
        }
    }

    private void BtnUndoStroke_Click(object sender, RoutedEventArgs e)
    {
        if (DetailInkCanvas.Strokes.Count > 0)
        {
            DetailInkCanvas.Strokes.RemoveAt(DetailInkCanvas.Strokes.Count - 1);
        }
    }

    private void BtnSaveAnnotations_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null || DetailImage.Source is not BitmapSource baseSource) return;

        try
        {
            int w = baseSource.PixelWidth;
            int h = baseSource.PixelHeight;

            // Render both image and ink canvas to a RenderTargetBitmap
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(baseSource, new Rect(0, 0, w, h));
                DetailInkCanvas.Strokes.Draw(dc);
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            // Save back to file
            ScreenCaptureHelper.SaveBitmapSourceToPng(rtb, _selectedItem.FilePath);

            // Reload UI
            _selectedItem.RefreshThumbnail();
            DetailImage.Source = ScreenCaptureHelper.LoadBitmapImageWithoutLock(_selectedItem.FilePath);
            DetailInkCanvas.Strokes.Clear();

            MessageBox.Show("Coretan berhasil disimpan ke gambar!", "DRANOTCH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal menyimpan coretan: {ex.Message}", "DRANOTCH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Zoom Controls

    private void ResetZoom()
    {
        _currentZoom = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        CanvasScaleTransform.ScaleX = _currentZoom;
        CanvasScaleTransform.ScaleY = _currentZoom;
        ZoomLevelText.Text = $"{(int)(_currentZoom * 100)}%";
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Min(3.0, _currentZoom + 0.15);
        ApplyZoom();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Max(0.2, _currentZoom - 0.15);
        ApplyZoom();
    }

    private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
    {
        if (DetailImage.Source is BitmapSource src && ImageScrollViewer.ActualWidth > 0 && ImageScrollViewer.ActualHeight > 0)
        {
            double scaleX = (ImageScrollViewer.ActualWidth - 40) / src.PixelWidth;
            double scaleY = (ImageScrollViewer.ActualHeight - 40) / src.PixelHeight;
            _currentZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.2, 1.5);
            ApplyZoom();
        }
    }

    #endregion

    #region Selected Item Actions

    private void DetailTitleBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitTitleChange();
    }

    private void DetailTitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTitleChange();
            Keyboard.ClearFocus();
        }
    }

    private void CommitTitleChange()
    {
        if (_selectedItem != null && !string.IsNullOrWhiteSpace(DetailTitleBox.Text))
        {
            _selectedItem.Title = DetailTitleBox.Text.Trim();
            _screenshotService.UpdateScreenshot(_selectedItem.Item);
        }
    }

    private void CopySelectedItemToClipboard()
    {
        if (_selectedItem != null && File.Exists(_selectedItem.FilePath))
        {
            try
            {
                var bmp = ScreenCaptureHelper.LoadBitmapImageWithoutLock(_selectedItem.FilePath);
                ScreenCaptureHelper.CopyToClipboardSafely(bmp);
            }
            catch { }
        }
    }

    private void BtnCopySelected_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedItemToClipboard();
    }

    private void BtnPinSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            _mainViewModel.PinScreenshot(_selectedItem.Item);
        }
    }

    private void SaveSelectedItemAsFile()
    {
        if (_selectedItem == null || !File.Exists(_selectedItem.FilePath)) return;

        var sfd = new SaveFileDialog
        {
            Title = "Simpan Tangkapan Layar Sebagai",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
            FileName = _selectedItem.FileName
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(_selectedItem.FilePath, sfd.FileName, overwrite: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menyimpan file: {ex.Message}", "DRANOTCH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSaveAsSelected_Click(object sender, RoutedEventArgs e)
    {
        SaveSelectedItemAsFile();
    }

    private void DeleteSelectedItem()
    {
        if (_selectedItem == null) return;

        var result = MessageBox.Show(
            $"Apakah Anda yakin ingin menghapus tangkapan layar '{_selectedItem.Title}'?",
            "Konfirmasi Hapus",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _screenshotService.DeleteScreenshot(_selectedItem.Id);
            ReloadScreenshots();
        }
    }

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedItem();
    }

    #endregion

    #region Toolbar Actions

    private void BtnNewSnip_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.StartSnipping();
    }

    private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.CaptureFullScreen();
    }

    private void BtnActiveWindow_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.CaptureActiveWindow();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private async void BtnSyncDrive_Click(object sender, RoutedEventArgs e)
    {
        if (BtnSyncDrive == null) return;
        var originalContent = BtnSyncDrive.Content;
        try
        {
            BtnSyncDrive.IsEnabled = false;
            BtnSyncDrive.Content = "⏳ Menyinkronkan...";

            await _mainViewModel.SyncScreenshotsWithGoogleDriveAsync();
            ReloadScreenshots();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sinkronisasi Google Drive gagal:\n{ex.Message}", "DRANOTCH - Google Drive Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BtnSyncDrive.IsEnabled = true;
            BtnSyncDrive.Content = originalContent;
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _screenshotService.GetScreenshotsFolder();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tidak dapat membuka folder:\n{ex.Message}", "DRANOTCH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportAll_Click(object sender, RoutedEventArgs e)
    {
        using var fbd = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Pilih folder tujuan untuk mengekspor semua gambar tangkapan layar",
            UseDescriptionForTitle = true
        };

        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            int count = _screenshotService.ExportAll(fbd.SelectedPath);
            MessageBox.Show(
                $"Berhasil mengekspor {count} gambar ke folder:\n{fbd.SelectedPath}",
                "DRANOTCH - Ekspor Berhasil",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (_allScreenshots.Count == 0) return;

        var result = MessageBox.Show(
            $"Apakah Anda yakin ingin menghapus SEMUA ({_allScreenshots.Count}) tangkapan layar yang tersimpan?\nTindakan ini tidak dapat dibatalkan.",
            "Konfirmasi Hapus Semua",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _screenshotService.ClearAllScreenshots();
            ReloadScreenshots();
        }
    }

    #endregion

    #region Window Controls

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            BtnMaximize.Content = "🗖";
            BtnMaximize.ToolTip = "Perbesar Jendela (Maximize)";
            RootBorder.Margin = new Thickness(10);
            RootBorder.CornerRadius = new CornerRadius(16);
        }
        else
        {
            WindowState = WindowState.Maximized;
            BtnMaximize.Content = "🗗";
            BtnMaximize.ToolTip = "Kembalikan Ukuran (Restore)";
            RootBorder.Margin = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(0);
        }
    }

    #region Window Resizing via 8 Thumbs

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (WindowState == WindowState.Normal && ActualWidth >= MinWidth && ActualHeight >= MinHeight)
        {
            _settingsService.CurrentSettings.ScreenshotHubWidth = ActualWidth;
            _settingsService.CurrentSettings.ScreenshotHubHeight = ActualHeight;
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
            Width = newWidth;
            Left += e.HorizontalChange;
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
            Width = newWidth;
            Left += e.HorizontalChange;
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
            Height = newHeight;
            Top += e.VerticalChange;
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
            Width = newWidth;
            Left += e.HorizontalChange;
        }
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
            Top += e.VerticalChange;
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
            Height = newHeight;
            Top += e.VerticalChange;
        }
    }

    #endregion

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _mainViewModel.IsScreenshotHubOpen = false;
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
            _mainViewModel.IsScreenshotHubOpen = false;
            return;
        }

        base.OnClosing(e);
    }

    #endregion
}
