using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NotchMusic.Helpers;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class NotificationToastWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosingOrHiding;

    public NotificationToastWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            WindowHelper.SetToolWindow(this);
            WindowHelper.CenterOnScreen(this);
        };

        SizeChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowHelper.CenterOnScreen(this);
            }
        };
    }

    public void ShowNotification()
    {
        _isClosingOrHiding = false;

        if (!IsVisible)
        {
            Opacity = 0.0;
            Show();
        }

        WindowHelper.CenterOnScreen(this);

        var fadeIn = new DoubleAnimation(Opacity, 1.0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HideNotification()
    {
        if (!IsVisible || _isClosingOrHiding) return;
        _isClosingOrHiding = true;

        var fadeOut = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (s, e) =>
        {
            if (_isClosingOrHiding)
            {
                Hide();
                _isClosingOrHiding = false;
            }
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
