namespace NotchMusic.Models;

public class LyricLine
{
    public TimeSpan Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;

    public LyricLine() { }

    public LyricLine(TimeSpan timestamp, string text)
    {
        Timestamp = timestamp;
        Text = text;
    }
}

public class SongLyrics
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public bool IsSynced { get; set; }
    public List<LyricLine> Lines { get; set; } = new();
    public string PlainLyrics { get; set; } = string.Empty;
    public TimeSpan Offset { get; set; } = TimeSpan.Zero;
    public string Source { get; set; } = "None"; // "LRCLIB", "Local LRC", "Local TXT", "Manual"
    public bool IsNotFound { get; set; }

    public static SongLyrics Empty(string message = "Lirik belum tersedia") => new()
    {
        IsNotFound = true,
        PlainLyrics = message
    };
}
