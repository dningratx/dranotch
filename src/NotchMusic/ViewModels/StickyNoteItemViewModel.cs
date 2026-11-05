using System.Windows.Input;
using System.Windows.Media;
using NotchMusic.Helpers;
using NotchMusic.Models;

namespace NotchMusic.ViewModels;

public class StickyNoteItemViewModel : ViewModelBase
{
    private readonly StickyNoteItem _model;

    public event Action? Changed;
    public event Action<StickyNoteItemViewModel>? DeleteRequested;
    public event Action<StickyNoteItemViewModel>? CloseRequested;
    public event Action<StickyNoteItemViewModel>? OpenRequested;

    public StickyNoteItemViewModel() : this(new StickyNoteItem())
    {
    }

    public StickyNoteItemViewModel(StickyNoteItem model)
    {
        _model = model ?? new StickyNoteItem();

        SetColorCommand = new RelayCommand(colorObj =>
        {
            if (colorObj is string color && !string.IsNullOrWhiteSpace(color))
            {
                Color = color;
            }
        });

        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        ToggleLockCommand = new RelayCommand(() => IsLocked = !IsLocked);
        ToggleCollapseCommand = new RelayCommand(() => IsCollapsed = !IsCollapsed);
        CloseNoteCommand = new RelayCommand(() => CloseRequested?.Invoke(this));
        DeleteNoteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this));
        OpenNoteCommand = new RelayCommand(() => OpenRequested?.Invoke(this));
    }

    public StickyNoteItem ToModel()
    {
        return new StickyNoteItem
        {
            Id = _model.Id,
            Title = _model.Title,
            Content = _model.Content,
            Color = _model.Color,
            IsPinned = _model.IsPinned,
            IsLocked = _model.IsLocked,
            IsCollapsed = _model.IsCollapsed,
            IsClosed = _model.IsClosed,
            Left = _model.Left,
            Top = _model.Top,
            Width = _model.Width,
            Height = _model.Height,
            CreatedAt = _model.CreatedAt,
            UpdatedAt = _model.UpdatedAt
        };
    }

    public string Id => _model.Id;

    public string Title
    {
        get => _model.Title;
        set
        {
            if (_model.Title != value)
            {
                _model.Title = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(LastEditedText));
                OnPropertyChanged(nameof(Snippet));
                Changed?.Invoke();
            }
        }
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(_model.Title) ? "Catatan Baru" : _model.Title;

    public string Content
    {
        get => _model.Content;
        set
        {
            if (_model.Content != value)
            {
                _model.Content = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(Content));
                OnPropertyChanged(nameof(Snippet));
                OnPropertyChanged(nameof(LastEditedText));
                OnPropertyChanged(nameof(CharCountText));
                Changed?.Invoke();
            }
        }
    }

    public string Color
    {
        get => string.IsNullOrWhiteSpace(_model.Color) ? "Yellow" : _model.Color;
        set
        {
            if (_model.Color != value)
            {
                _model.Color = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(Color));
                OnPropertyChanged(nameof(ColorNameIndonesia));
                OnPropertyChanged(nameof(HeaderBackgroundBrush));
                OnPropertyChanged(nameof(CardBackgroundBrush));
                OnPropertyChanged(nameof(TextForegroundBrush));
                OnPropertyChanged(nameof(SecondaryTextBrush));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(ColorDotBrush));
                Changed?.Invoke();
            }
        }
    }

    public bool IsPinned
    {
        get => _model.IsPinned;
        set
        {
            if (_model.IsPinned != value)
            {
                _model.IsPinned = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(IsPinned));
                OnPropertyChanged(nameof(PinOpacity));
                Changed?.Invoke();
            }
        }
    }

    public double PinOpacity => _model.IsPinned ? 1.0 : 0.35;

    public bool IsLocked
    {
        get => _model.IsLocked;
        set
        {
            if (_model.IsLocked != value)
            {
                _model.IsLocked = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(IsLocked));
                Changed?.Invoke();
            }
        }
    }

    public bool IsCollapsed
    {
        get => _model.IsCollapsed;
        set
        {
            if (_model.IsCollapsed != value)
            {
                _model.IsCollapsed = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(IsCollapsed));
                Changed?.Invoke();
            }
        }
    }

    public bool IsClosed
    {
        get => _model.IsClosed;
        set
        {
            if (_model.IsClosed != value)
            {
                _model.IsClosed = value;
                _model.UpdatedAt = DateTime.Now;
                OnPropertyChanged(nameof(IsClosed));
                OnPropertyChanged(nameof(IsOpenOnDesktop));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBackgroundBrush));
                OnPropertyChanged(nameof(StatusForegroundBrush));
                OnPropertyChanged(nameof(ActionButtonText));
                Changed?.Invoke();
            }
        }
    }

    public bool IsOpenOnDesktop => !_model.IsClosed;

    public string StatusText => IsOpenOnDesktop ? "Di Layar" : "Tersimpan";
    public System.Windows.Media.Brush StatusBackgroundBrush => IsOpenOnDesktop
        ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0x38, 0xBD, 0xF8))
        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
    public System.Windows.Media.Brush StatusForegroundBrush => IsOpenOnDesktop
        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8))
        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
    public string ActionButtonText => IsOpenOnDesktop ? "Fokus" : "Buka";

    public double Left
    {
        get => _model.Left;
        set
        {
            if (Math.Abs(_model.Left - value) > 1.0)
            {
                _model.Left = value;
                OnPropertyChanged(nameof(Left));
                Changed?.Invoke();
            }
        }
    }

    public double Top
    {
        get => _model.Top;
        set
        {
            if (Math.Abs(_model.Top - value) > 1.0)
            {
                _model.Top = value;
                OnPropertyChanged(nameof(Top));
                Changed?.Invoke();
            }
        }
    }

    public double Width
    {
        get => _model.Width < 200 ? 320 : _model.Width;
        set
        {
            if (Math.Abs(_model.Width - value) > 1.0)
            {
                _model.Width = Math.Max(200, value);
                OnPropertyChanged(nameof(Width));
                Changed?.Invoke();
            }
        }
    }

    public double Height
    {
        get => _model.Height < 160 ? 320 : _model.Height;
        set
        {
            if (Math.Abs(_model.Height - value) > 1.0)
            {
                _model.Height = Math.Max(160, value);
                OnPropertyChanged(nameof(Height));
                Changed?.Invoke();
            }
        }
    }

    public DateTime CreatedAt => _model.CreatedAt;
    public DateTime UpdatedAt
    {
        get => _model.UpdatedAt;
        set
        {
            _model.UpdatedAt = value;
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(LastEditedText));
        }
    }

    public string Snippet
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_model.Content))
                return "(Catatan kosong)";

            var text = _model.Content.Trim();
            var firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
            if (firstLine.Length > 100)
                return firstLine[..100] + "...";
            return firstLine;
        }
    }

    public string LastEditedText
    {
        get
        {
            var diff = DateTime.Now - _model.UpdatedAt;
            if (diff.TotalMinutes < 1) return "Baru saja diedit";
            if (diff.TotalHours < 1) return $"{Math.Max(1, (int)diff.TotalMinutes)} mnt lalu";
            if (diff.TotalDays < 1) return $"Diedit hari ini {_model.UpdatedAt:HH:mm}";
            if (diff.TotalDays < 2) return $"Diedit kemarin {_model.UpdatedAt:HH:mm}";
            return $"Diedit {_model.UpdatedAt:dd MMM yyyy}";
        }
    }

    public string CharCountText
    {
        get
        {
            var len = _model.Content?.Length ?? 0;
            return $"{len} karakter";
        }
    }

    public string ColorNameIndonesia => Color.ToLowerInvariant() switch
    {
        "yellow" => "Kuning",
        "green" => "Hijau",
        "pink" => "Pink",
        "blue" => "Biru",
        "purple" => "Ungu",
        "orange" => "Oranye",
        "dark" => "Gelap",
        _ => "Kuning"
    };

    // UI Color Palettes (Sleek pastel & dark aesthetics)
    public SolidColorBrush CardBackgroundBrush => Color.ToLowerInvariant() switch
    {
        "yellow" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFDE7")),
        "green" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F5E9")),
        "pink" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCE4EC")),
        "blue" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD")),
        "purple" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3E5F5")),
        "orange" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF3E0")),
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C1C24")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFDE7"))
    };

    public SolidColorBrush HeaderBackgroundBrush => Color.ToLowerInvariant() switch
    {
        "yellow" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF59D")),
        "green" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C8E6C9")),
        "pink" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8BBD0")),
        "blue" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BBDEFB")),
        "purple" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E1BEE7")),
        "orange" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFE0B2")),
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#272733")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF59D"))
    };

    public SolidColorBrush TextForegroundBrush => Color.ToLowerInvariant() switch
    {
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5FA")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#212121"))
    };

    public SolidColorBrush SecondaryTextBrush => Color.ToLowerInvariant() switch
    {
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9A9AB0")),
        "yellow" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#757575")),
        "green" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32")),
        "pink" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C2185B")),
        "blue" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0")),
        "purple" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7B1FA2")),
        "orange" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E65100")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#616161"))
    };

    public SolidColorBrush BorderBrush => Color.ToLowerInvariant() switch
    {
        "yellow" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FBC02D")),
        "green" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#81C784")),
        "pink" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F06292")),
        "blue" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64B5F6")),
        "purple" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BA68C8")),
        "orange" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB74D")),
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E3E50")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FBC02D"))
    };

    public SolidColorBrush AccentBrush => Color.ToLowerInvariant() switch
    {
        "yellow" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F57F17")),
        "green" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32")),
        "pink" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D81B60")),
        "blue" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2")),
        "purple" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8E24AA")),
        "orange" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF6C00")),
        "dark" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#818CF8")),
        _ => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F57F17"))
    };

    public SolidColorBrush ColorDotBrush => HeaderBackgroundBrush;

    public ICommand SetColorCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand ToggleLockCommand { get; }
    public ICommand ToggleCollapseCommand { get; }
    public ICommand CloseNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }
    public ICommand OpenNoteCommand { get; }
}
