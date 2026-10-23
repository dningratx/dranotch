using System.Windows;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        try
        {
            var uri = new Uri("pack://application:,,,/NotchMusic;component/Assets/app.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenYouTubeMusic_Click(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mainWin)
        {
            mainWin.OpenYouTubeMusic();
        }
        else
        {
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MainWindow mw)
                {
                    mw.OpenYouTubeMusic();
                    break;
                }
            }
        }
    }

    private void OpenFloatingBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mainWin)
        {
            mainWin.OpenFloatingBrowser();
        }
        else
        {
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MainWindow mw)
                {
                    mw.OpenFloatingBrowser();
                    break;
                }
            }
        }
    }
}
