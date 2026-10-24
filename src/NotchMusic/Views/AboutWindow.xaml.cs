using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => WindowHelper.SetToolWindow(this);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OpenReleasePage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://dningratx.github.io/dranotch/")
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
