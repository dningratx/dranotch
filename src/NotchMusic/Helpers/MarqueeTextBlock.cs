using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using TranslateTransform = System.Windows.Media.TranslateTransform;
using Visual = System.Windows.Media.Visual;
using ICommand = System.Windows.Input.ICommand;

namespace NotchMusic.Helpers;

public enum MarqueeDirection
{
    RightToLeft,
    LeftToRight
}

/// <summary>
/// Kontrol teks berjalan (marquee) horizontal dari kanan ke kiri (default).
/// Memastikan seluruh teks hingga kata terakhir masuk ke sebelah kiri sebelum memicu ticker berikutnya.
/// </summary>
public class MarqueeTextBlock : FrameworkElement
{
    private readonly TextBlock _textBlock;
    private readonly TranslateTransform _transform;
    private Storyboard? _storyboard;
    private bool _isPaused;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, OnAppearanceChanged));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(11.5, FrameworkPropertyMetadataOptions.AffectsMeasure, OnAppearanceChanged));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(MarqueeTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnAppearanceChanged));

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(nameof(Speed), typeof(double), typeof(MarqueeTextBlock),
            new PropertyMetadata(36.0, OnSpeedChanged));

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(nameof(Direction), typeof(MarqueeDirection), typeof(MarqueeTextBlock),
            new PropertyMetadata(MarqueeDirection.RightToLeft, OnAppearanceChanged));

    public static readonly DependencyProperty CompletedCommandProperty =
        DependencyProperty.Register(nameof(CompletedCommand), typeof(ICommand), typeof(MarqueeTextBlock),
            new PropertyMetadata(null));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public MarqueeDirection Direction
    {
        get => (MarqueeDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public ICommand? CompletedCommand
    {
        get => (ICommand?)GetValue(CompletedCommandProperty);
        set => SetValue(CompletedCommandProperty, value);
    }

    public MarqueeTextBlock()
    {
        ClipToBounds = true;
        _transform = new TranslateTransform();
        _textBlock = new TextBlock
        {
            RenderTransform = _transform,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        };

        AddVisualChild(_textBlock);
        AddLogicalChild(_textBlock);

        Loaded += (s, e) => RestartAnimation(initialStartAtZero: true);
        Unloaded += (s, e) => StopAnimation();
        SizeChanged += (s, e) => RestartAnimation(initialStartAtZero: true);

        MouseEnter += (s, e) => Pause();
        MouseLeave += (s, e) => Resume();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
    {
        return index == 0 ? _textBlock : throw new ArgumentOutOfRangeException(nameof(index));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _textBlock.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        double height = Math.Max(16, _textBlock.DesiredSize.Height);
        double width = double.IsPositiveInfinity(availableSize.Width) ? _textBlock.DesiredSize.Width : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _textBlock.Arrange(new Rect(0, (finalSize.Height - _textBlock.DesiredSize.Height) / 2, _textBlock.DesiredSize.Width, _textBlock.DesiredSize.Height));
        return finalSize;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeTextBlock control)
        {
            control._textBlock.Text = e.NewValue as string ?? string.Empty;
            control._textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            control.RestartAnimation(initialStartAtZero: true);
        }
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeTextBlock control)
        {
            control._textBlock.Foreground = control.Foreground;
            control._textBlock.FontSize = control.FontSize;
            control._textBlock.FontWeight = control.FontWeight;
            control._textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            control.RestartAnimation(initialStartAtZero: true);
        }
    }

    private static void OnSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeTextBlock control)
        {
            control.RestartAnimation(initialStartAtZero: true);
        }
    }

    public void RestartAnimation(bool initialStartAtZero = false)
    {
        if (!IsLoaded || ActualWidth <= 0) return;

        StopAnimation();

        double containerWidth = ActualWidth;
        double textWidth = _textBlock.DesiredSize.Width;
        if (string.IsNullOrWhiteSpace(Text) || textWidth <= 0) return;

        _storyboard = new Storyboard();

        double fromX;
        double toX;
        TimeSpan beginTime = TimeSpan.Zero;

        if (Direction == MarqueeDirection.RightToLeft)
        {
            // Gerak dari kanan ke kiri: X berkurang (<-)
            // Jika initialStartAtZero: mulai di X = 0 (terbaca langsung), beri jeda sejenak (0.5s),
            // lalu berjalan hingga kata terakhir benar-benar masuk ke sebelah kiri (-textWidth - 14)
            if (initialStartAtZero)
            {
                fromX = 0;
                toX = -textWidth - 14;
                beginTime = TimeSpan.FromSeconds(0.5);
            }
            else
            {
                fromX = containerWidth;
                toX = -textWidth - 14;
            }
        }
        else
        {
            // Gerak dari kiri ke kanan
            fromX = initialStartAtZero ? 0 : -textWidth - 14;
            toX = containerWidth;
        }

        double distance = Math.Abs(toX - fromX);
        double speed = Math.Max(10.0, Speed);
        double seconds = Math.Max(2.0, distance / speed);

        var anim = new DoubleAnimation
        {
            From = fromX,
            To = toX,
            BeginTime = beginTime,
            Duration = TimeSpan.FromSeconds(seconds),
            FillBehavior = FillBehavior.Stop
        };

        Storyboard.SetTarget(anim, _textBlock);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        anim.Completed += (s, e) =>
        {
            if (!IsLoaded || !IsVisible) return;

            // Beri jeda 200ms setelah kata terakhir masuk ke sebelah kiri sebelum memunculkan ticker selanjutnya
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (ts, te) =>
            {
                timer.Stop();
                if (!IsLoaded || !IsVisible) return;

                string oldText = Text;
                if (CompletedCommand != null && CompletedCommand.CanExecute(null))
                {
                    CompletedCommand.Execute(null);
                }

                // Jika teks tidak berubah (misal hanya 1 tugas yang ada), putar siklus loop berikutnya
                if (Text == oldText)
                {
                    StartLoopAnimation(textWidth, containerWidth);
                }
            };
            timer.Start();
        };

        _storyboard.Children.Add(anim);
        _storyboard.Begin(this, isControllable: true);
        _isPaused = false;
    }

    private void StartLoopAnimation(double textWidth, double containerWidth)
    {
        if (!IsLoaded || ActualWidth <= 0) return;

        StopAnimation();

        _storyboard = new Storyboard();

        double fromX;
        double toX;

        if (Direction == MarqueeDirection.RightToLeft)
        {
            // Masuk dari sisi kanan (containerWidth) hingga seluruh kata terakhir masuk ke sebelah kiri (-textWidth - 14)
            fromX = containerWidth;
            toX = -textWidth - 14;
        }
        else
        {
            fromX = -textWidth - 14;
            toX = containerWidth;
        }

        double distance = Math.Abs(toX - fromX);
        double speed = Math.Max(10.0, Speed);
        double seconds = Math.Max(2.5, distance / speed);

        var loopAnim = new DoubleAnimation
        {
            From = fromX,
            To = toX,
            Duration = TimeSpan.FromSeconds(seconds),
            FillBehavior = FillBehavior.Stop
        };

        Storyboard.SetTarget(loopAnim, _textBlock);
        Storyboard.SetTargetProperty(loopAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        loopAnim.Completed += (s, e) =>
        {
            if (!IsLoaded || !IsVisible) return;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (ts, te) =>
            {
                timer.Stop();
                if (!IsLoaded || !IsVisible) return;

                string oldText = Text;
                if (CompletedCommand != null && CompletedCommand.CanExecute(null))
                {
                    CompletedCommand.Execute(null);
                }

                if (Text == oldText)
                {
                    StartLoopAnimation(textWidth, containerWidth);
                }
            };
            timer.Start();
        };

        _storyboard.Children.Add(loopAnim);
        _storyboard.Begin(this, isControllable: true);
        _isPaused = false;
    }

    public void Pause()
    {
        if (_storyboard != null && !_isPaused)
        {
            _storyboard.Pause(this);
            _isPaused = true;
        }
    }

    public void Resume()
    {
        if (_storyboard != null && _isPaused)
        {
            _storyboard.Resume(this);
            _isPaused = false;
        }
    }

    public void StopAnimation()
    {
        if (_storyboard != null)
        {
            _storyboard.Stop(this);
            _storyboard = null;
            _isPaused = false;
        }
        _transform.X = 0;
    }
}
