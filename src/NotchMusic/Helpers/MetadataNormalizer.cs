using System.Text.RegularExpressions;

namespace NotchMusic.Helpers;

public static class MetadataNormalizer
{
    // Cautious normalization: strips common noise while preserving remixes/live/acoustic tags
    // e.g., "Song Name (Official Music Video)" -> "Song Name"
    // "Song Name (Lyric Video)" -> "Song Name"
    // "Song Name [MV]" -> "Song Name"
    // But keeps "(Acoustic)", "(Live)", "(Remix)"
    private static readonly Regex VideoNoiseRegex = new(
        @"\s*[\(\[](?:official\s*(?:music\s*)?video|official\s*audio|visualizer|lyric\s*video|audio|mv|hd|hq)[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static (string CleanTitle, string CleanArtist) Normalize(string rawTitle, string rawArtist)
    {
        var title = rawTitle ?? string.Empty;
        var artist = rawArtist ?? string.Empty;

        // If artist is empty and title contains " - ", try splitting (common in YouTube video titles)
        if (string.IsNullOrWhiteSpace(artist) && title.Contains(" - "))
        {
            var parts = title.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                artist = parts[0].Trim();
                title = parts[1].Trim();
            }
        }

        // Strip video noise from title
        title = VideoNoiseRegex.Replace(title, "").Trim();

        // Normalize multiple spaces
        title = WhitespaceRegex.Replace(title, " ").Trim();
        artist = WhitespaceRegex.Replace(artist, " ").Trim();

        return (title, artist);
    }

    public static string CreateCacheKey(string title, string artist, TimeSpan duration)
    {
        var (cTitle, cArtist) = Normalize(title, artist);
        var durSec = (int)Math.Round(duration.TotalSeconds);
        return $"{cTitle.ToLowerInvariant()}___{cArtist.ToLowerInvariant()}___{durSec}";
    }
}
