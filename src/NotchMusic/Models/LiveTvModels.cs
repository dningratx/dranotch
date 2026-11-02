using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NotchMusic.Models;

public enum LiveTvPlaybackState
{
    Stopped,
    Buffering,
    Playing,
    Paused,
    Error
}

public class LiveTvCountryOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Flag => LiveTvChannel.GetFlagEmoji(Code);
    public string DisplayName => string.IsNullOrEmpty(Flag) ? Name : $"{Flag} {Name}";
    public override string ToString() => DisplayName;
}

public class LiveTvChannel : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _category = "Indonesia";
    private string _countryCode = "id";
    private string _countryName = "Indonesia";
    private string _streamUrl = string.Empty;
    private List<string> _fallbackUrls = new();
    private int _activeSourceIndex = 0;
    private bool _isGeoBlocked;
    private bool _isFavorite;
    private bool _isCustom;
    private bool _isCurrent;
    private bool _isPlaying;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(Subtitle)); }
    }

    public string CountryCode
    {
        get => _countryCode;
        set
        {
            _countryCode = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CountryFlag));
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string CountryName
    {
        get => _countryName;
        set { _countryName = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(Subtitle)); }
    }

    public string StreamUrl
    {
        get => _streamUrl;
        set
        {
            _streamUrl = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveStreamUrl));
            OnPropertyChanged(nameof(SourceBadgeText));
            OnPropertyChanged(nameof(IsYouTubeStream));
        }
    }

    public List<string> FallbackUrls
    {
        get => _fallbackUrls;
        set
        {
            _fallbackUrls = value ?? new();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveStreamUrl));
            OnPropertyChanged(nameof(SourceCount));
            OnPropertyChanged(nameof(HasMultipleSources));
            OnPropertyChanged(nameof(SourceBadgeText));
        }
    }

    [JsonIgnore]
    public int ActiveSourceIndex
    {
        get => _activeSourceIndex;
        set
        {
            _activeSourceIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveStreamUrl));
            OnPropertyChanged(nameof(SourceBadgeText));
            OnPropertyChanged(nameof(IsYouTubeStream));
        }
    }

    public bool IsGeoBlocked
    {
        get => _isGeoBlocked;
        set { _isGeoBlocked = value; OnPropertyChanged(); }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set { _isFavorite = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteSymbol)); }
    }

    public bool IsCustom
    {
        get => _isCustom;
        set { _isCustom = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public List<string> AllSources
    {
        get
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(_streamUrl))
            {
                list.Add(_streamUrl.Trim());
            }
            if (_fallbackUrls != null)
            {
                foreach (var fb in _fallbackUrls)
                {
                    if (!string.IsNullOrWhiteSpace(fb) && !list.Contains(fb.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(fb.Trim());
                    }
                }
            }
            return list;
        }
    }

    [JsonIgnore]
    public int SourceCount => AllSources.Count;

    [JsonIgnore]
    public bool HasMultipleSources => SourceCount > 1;

    [JsonIgnore]
    public string ActiveStreamUrl
    {
        get
        {
            var all = AllSources;
            if (all.Count == 0) return string.Empty;
            int idx = Math.Clamp(_activeSourceIndex, 0, all.Count - 1);
            return all[idx];
        }
    }

    [JsonIgnore]
    public bool IsYouTubeStream => IsYouTubeUrl(ActiveStreamUrl);

    [JsonIgnore]
    public string CountryFlag => GetFlagEmoji(_countryCode);

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            var flagPrefix = string.IsNullOrEmpty(CountryFlag) ? "" : $"{CountryFlag} ";
            if (!string.IsNullOrWhiteSpace(CountryName) && !string.IsNullOrWhiteSpace(Category) &&
                !string.Equals(CountryName, Category, StringComparison.OrdinalIgnoreCase))
            {
                return $"{flagPrefix}{CountryName} • {Category}";
            }
            if (!string.IsNullOrWhiteSpace(CountryName))
            {
                return $"{flagPrefix}{CountryName}";
            }
            return Category;
        }
    }

    [JsonIgnore]
    public string SourceBadgeText
    {
        get
        {
            var typeLabel = IsYouTubeStream ? "YT" : "HLS";
            var count = SourceCount;
            if (count > 1)
            {
                int current = Math.Clamp(_activeSourceIndex, 0, count - 1) + 1;
                return $"{typeLabel} {current}/{count}";
            }
            return typeLabel;
        }
    }

    [JsonIgnore]
    public string FavoriteSymbol => IsFavorite ? "★" : "☆";

    public static bool IsYouTubeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube-nocookie.com/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFlagEmoji(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return "🌍";
        var code = countryCode.Trim().ToUpperInvariant();
        if (code == "ALL" || code == "GLOBAL" || code == "INTL") return "🌍";
        if (code.Length == 2 && code[0] is >= 'A' and <= 'Z' && code[1] is >= 'A' and <= 'Z')
        {
            return char.ConvertFromUtf32(0x1F1E6 + (code[0] - 'A')) +
                   char.ConvertFromUtf32(0x1F1E6 + (code[1] - 'A'));
        }
        return "📺";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
