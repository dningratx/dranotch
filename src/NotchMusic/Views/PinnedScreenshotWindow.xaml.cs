using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Screenshot;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace NotchMusic.Views;

public partial class PinnedScreenshotWindow : Window
{
    private readonly ScreenshotItem _item;
    private BitmapImage? _bitmapImage;

    public string ScreenshotId => _item.Id;

    public PinnedScreenshotWindow(ScreenshotItem item)
    {
        InitializeComponent();
        _item = item;

        Loaded += PinnedScreenshotWindow_Loaded;
        MouseEnter += (s, e) => HoverToolbar.Visibility = Visibility.Visible;
        MouseLeave += (s, e) => HoverToolbar.Visibility = Visibility.Collapsed;
        MouseDown += PinnedScreenshotWindow_MouseDown;
        MouseWheel += PinnedScreenshotWindow_MouseWheel;
    }

    private void PinnedScreenshotWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        if (File.Exists(_item.FilePath))
        {
            try
            {
                _bitmapImage = ScreenCaptureHelper.LoadBitmapImageWithoutLock(_item.FilePath);
                PinImage.Source = _bitmapImage;

                // Adjust window aspect ratio based on image
                if (_item.Width > 0 && _item.Height > 0)
                {
                    double targetW = Math.Clamp(_item.Width, 240, 640);
                    double ratio = (double)_item.Height / _item.Width;
                    Width = targetW + 20;
                    Height = (targetW * ratio) + 20;
                }
            }
            catch { }
        }

        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void PinnedScreenshotWindow_MouseDown(object sender, MouseButtonEventArgs e)
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

    private void PinnedScreenshotWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Adjust opacity on Ctrl + Wheel or plain wheel
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Zoom / Resize window
            double scale = e.Delta > 0 ? 1.08 : 0.92;
            Width = Math.Max(MinWidth, Width * scale);
            Height = Math.Max(MinHeight, Height * scale);
        }
        else
        {
            // Adjust opacity between 0.2 and 1.0
            double delta = e.Delta > 0 ? 0.05 : -0.05;
            Opacity = Math.Clamp(Opacity + delta, 0.25, 1.0);
        }
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmapImage != null)
        {
            ScreenCaptureHelper.CopyToClipboardSafely(_bitmapImage);
        }
    }

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_item.FilePath)) return;

        var sfd = new SaveFileDialog
        {
            Title = "Simpan Tangkapan Layar Sebagai",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
            FileName = _item.FileName
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(_item.FilePath, sfd.FileName, overwrite: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menyimpan file: {ex.Message}", "DRANOTCH - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
