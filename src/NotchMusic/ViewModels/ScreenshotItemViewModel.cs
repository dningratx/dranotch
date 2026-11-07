using System.IO;
using System.Windows.Media.Imaging;
using NotchMusic.Models;
using NotchMusic.Services.Screenshot;

namespace NotchMusic.ViewModels;

public class ScreenshotItemViewModel : ViewModelBase
{
    private readonly ScreenshotItem _item;
    private BitmapImage? _thumbnail;
    private bool _isSelected;

    public ScreenshotItem Item => _item;

    public string Id => _item.Id;
    public string FileName => _item.FileName;
    public string FilePath => _item.FilePath;
    public DateTime CreatedAt => _item.CreatedAt;
    public int Width => _item.Width;
    public int Height => _item.Height;
    public long FileSize => _item.FileSize;
    public string CaptureMode => _item.CaptureMode;

    public string Title
    {
        get => _item.Title;
        set
        {
            if (_item.Title != value)
            {
                _item.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Note
    {
        get => _item.Note;
        set
        {
            if (_item.Note != value)
            {
                _item.Note = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPinned
    {
        get => _item.IsPinned;
        set
        {
            if (_item.IsPinned != value)
            {
                _item.IsPinned = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string FormattedDate
    {
        get
        {
            var now = DateTime.Now;
            if (_item.CreatedAt.Date == now.Date)
            {
                return $"Hari ini, {_item.CreatedAt:HH:mm:ss}";
            }
            if (_item.CreatedAt.Date == now.Date.AddDays(-1))
            {
                return $"Kemarin, {_item.CreatedAt:HH:mm:ss}";
            }
            return _item.CreatedAt.ToString("dd MMM yyyy, HH:mm");
        }
    }

    public string FormattedResolution => $"{Width} × {Height} px";

    public string FormattedFileSize
    {
        get
        {
            if (FileSize <= 0) return "0 KB";
            if (FileSize < 1024 * 1024)
            {
                return $"{(FileSize / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} KB";
            }
            return $"{(FileSize / (1024.0 * 1024.0)).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} MB";
        }
    }

    public string ModeLabel => CaptureMode switch
    {
        "FullScreen" => "Layar Penuh",
        "ActiveWindow" => "Jendela",
        _ => "Area / Kotak"
    };

    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumbnail == null && File.Exists(_item.FilePath))
            {
                try
                {
                    _thumbnail = ScreenCaptureHelper.LoadBitmapImageWithoutLock(_item.FilePath);
                }
                catch
                {
                    // Ignore load error
                }
            }
            return _thumbnail;
        }
    }

    public void RefreshThumbnail()
    {
        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
    }

    public ScreenshotItemViewModel(ScreenshotItem item)
    {
        _item = item;
    }
}
