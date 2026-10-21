using System.Windows.Media.Imaging;

namespace NotchMusic.Models;

public class TrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public bool IsPlaying { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
    public BitmapImage? Thumbnail { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string SourceAppId { get; set; } = string.Empty;
    public bool CanPlayPause { get; set; }
    public bool CanPrevious { get; set; }
    public bool CanNext { get; set; }
    public bool CanSeek { get; set; }

    public string UniqueSongKey => $"{NormalizeForComparison(Title)}___{NormalizeForComparison(Artist)}";

    private static string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Trim().ToLowerInvariant();
    }
}
