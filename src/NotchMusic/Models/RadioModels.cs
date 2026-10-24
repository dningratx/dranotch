using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NotchMusic.Models;

public enum RadioPlaybackState
{
    Stopped,
    Buffering,
    Playing,
    Paused,
    Error
}

public class RadioStation : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _category = "Indonesia";
    private string _streamUrl = string.Empty;
    private List<string> _fallbackUrls = new();
    private string _cityOrCountry = string.Empty;
    private string _genre = string.Empty;
    private int _bitrate = 128;
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
        set { _category = value; OnPropertyChanged(); }
    }

    public string StreamUrl
    {
        get => _streamUrl;
        set { _streamUrl = value; OnPropertyChanged(); }
    }

    public List<string> FallbackUrls
    {
        get => _fallbackUrls;
        set { _fallbackUrls = value ?? new(); OnPropertyChanged(); }
    }

    public string CityOrCountry
    {
        get => _cityOrCountry;
        set { _cityOrCountry = value; OnPropertyChanged(); OnPropertyChanged(nameof(Subtitle)); }
    }

    public string Genre
    {
        get => _genre;
        set { _genre = value; OnPropertyChanged(); OnPropertyChanged(nameof(Subtitle)); }
    }

    public int Bitrate
    {
        get => _bitrate;
        set { _bitrate = value; OnPropertyChanged(); OnPropertyChanged(nameof(BitrateText)); }
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
    public string Subtitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CityOrCountry) && !string.IsNullOrWhiteSpace(Genre))
                return $"{CityOrCountry} • {Genre}";
            if (!string.IsNullOrWhiteSpace(CityOrCountry))
                return CityOrCountry;
            if (!string.IsNullOrWhiteSpace(Genre))
                return Genre;
            return Category;
        }
    }

    [JsonIgnore]
    public string BitrateText => Bitrate > 0 ? $"{Bitrate} kbps" : "LIVE";

    [JsonIgnore]
    public string FavoriteSymbol => IsFavorite ? "★" : "☆";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
