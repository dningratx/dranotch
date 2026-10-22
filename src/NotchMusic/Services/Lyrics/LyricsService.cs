using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Lyrics;

public interface ILyricsService
{
    Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default);
    SongLyrics LoadFromLocalFile(string filePath);
}

public class LrcLibSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("albumName")]
    public string? AlbumName { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }

    [JsonPropertyName("instrumental")]
    public bool? Instrumental { get; set; }
}

public class LyricsService : ILyricsService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    private readonly ISettingsService _settingsService;

    static LyricsService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DRANOTCH/1.0 (https://github.com/dranotch/dranotch)");
    }

    public LyricsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<SongLyrics> GetLyricsAsync(string rawTitle, string rawArtist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var (cleanTitle, cleanArtist) = MetadataNormalizer.Normalize(rawTitle, rawArtist);
        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            return SongLyrics.Empty("Tidak ada informasi lagu");
        }

        // Check local cache first
        var cacheKey = MetadataNormalizer.CreateCacheKey(cleanTitle, cleanArtist, duration);
        var cachedLyrics = TryGetFromCache(cacheKey);
        if (cachedLyrics != null && (cachedLyrics.IsSynced || cachedLyrics.Source.Contains("Instrumental") || cachedLyrics.Source.StartsWith("Local")))
        {
            return cachedLyrics;
        }

        // Keep cached plain lyrics as fallback if available, while attempting to fetch synced lyrics online
        SongLyrics? fallbackPlainLyrics = cachedLyrics;

        // 1. Try LRCLIB /api/get endpoint (exact match)
        try
        {
            var durSec = (int)Math.Round(duration.TotalSeconds);
            var url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(cleanArtist)}";
            if (durSec > 10)
            {
                url += $"&duration={durSec}";
            }
            if (!string.IsNullOrWhiteSpace(album))
            {
                url += $"&album_name={Uri.EscapeDataString(album)}";
            }

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var item = JsonSerializer.Deserialize<LrcLibSearchResult>(json);
                if (item != null)
                {
                    if (item.Instrumental == true || !string.IsNullOrWhiteSpace(item.SyncedLyrics))
                    {
                        var result = ParseLrcLibItem(item, cleanTitle, cleanArtist);
                        SaveToCache(cacheKey, result);
                        return result;
                    }
                    else if (!string.IsNullOrWhiteSpace(item.PlainLyrics))
                    {
                        fallbackPlainLyrics = ParseLrcLibItem(item, cleanTitle, cleanArtist);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fall back to /api/search
        }

        // 2. Try search endpoint to find synced lyrics (or if exact get didn't have synced lyrics)
        try
        {
            var searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString($"{cleanTitle} {cleanArtist}".Trim())}";
            using var searchResp = await HttpClient.GetAsync(searchUrl, cancellationToken);
            if (searchResp.IsSuccessStatusCode)
            {
                var json = await searchResp.Content.ReadAsStringAsync(cancellationToken);
                var items = JsonSerializer.Deserialize<List<LrcLibSearchResult>>(json);
                if (items != null && items.Count > 0)
                {
                    // Look for synced lyrics or instrumental first
                    var syncedItems = items.Where(i => i.Instrumental == true || !string.IsNullOrWhiteSpace(i.SyncedLyrics)).ToList();
                    if (syncedItems.Count > 0)
                    {
                        var bestSynced = syncedItems
                            .OrderBy(i => duration.TotalSeconds > 0 && i.Duration.HasValue ? Math.Abs(i.Duration.Value - duration.TotalSeconds) : 0)
                            .First();

                        var result = ParseLrcLibItem(bestSynced, cleanTitle, cleanArtist);
                        SaveToCache(cacheKey, result);
                        return result;
                    }

                    if (fallbackPlainLyrics == null)
                    {
                        var plainItems = items.Where(i => !string.IsNullOrWhiteSpace(i.PlainLyrics)).ToList();
                        if (plainItems.Count > 0)
                        {
                            var bestPlain = plainItems
                                .OrderBy(i => duration.TotalSeconds > 0 && i.Duration.HasValue ? Math.Abs(i.Duration.Value - duration.TotalSeconds) : 0)
                                .First();
                            fallbackPlainLyrics = ParseLrcLibItem(bestPlain, cleanTitle, cleanArtist);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // API network failure
        }

        if (fallbackPlainLyrics != null)
        {
            SaveToCache(cacheKey, fallbackPlainLyrics);
            return fallbackPlainLyrics;
        }

        return SongLyrics.Empty("Lirik belum tersedia");
    }

    public async Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<LrcLibSearchResult>();

        try
        {
            var url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
            using var resp = await HttpClient.GetAsync(url, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                var items = JsonSerializer.Deserialize<List<LrcLibSearchResult>>(json);
                if (items != null)
                {
                    return items
                        .OrderByDescending(i => !string.IsNullOrEmpty(i.SyncedLyrics) || i.Instrumental == true)
                        .ToList();
                }
            }
        }
        catch
        {
        }
        return new List<LrcLibSearchResult>();
    }

    public SongLyrics LoadFromLocalFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return SongLyrics.Empty("File tidak ditemukan");
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var content = File.ReadAllText(filePath, Encoding.UTF8);

        if (ext == ".lrc")
        {
            var parsed = LrcParser.Parse(content);
            parsed.Source = $"Local file: {Path.GetFileName(filePath)}";
            return parsed;
        }
        else
        {
            // .txt or generic
            return new SongLyrics
            {
                IsSynced = false,
                PlainLyrics = content,
                Source = $"Local file: {Path.GetFileName(filePath)}"
            };
        }
    }

    private static SongLyrics ParseLrcLibItem(LrcLibSearchResult item, string defaultTitle, string defaultArtist)
    {
        if (item.Instrumental == true)
        {
            return new SongLyrics
            {
                Title = item.TrackName ?? defaultTitle,
                Artist = item.ArtistName ?? defaultArtist,
                IsSynced = false,
                PlainLyrics = "♪ Instrumental ♪",
                Source = "LRCLIB (Instrumental)"
            };
        }

        if (!string.IsNullOrWhiteSpace(item.SyncedLyrics))
        {
            var lyrics = LrcParser.Parse(item.SyncedLyrics);
            lyrics.Title = item.TrackName ?? defaultTitle;
            lyrics.Artist = item.ArtistName ?? defaultArtist;
            lyrics.Source = "LRCLIB (Synced)";
            return lyrics;
        }

        if (!string.IsNullOrWhiteSpace(item.PlainLyrics))
        {
            return new SongLyrics
            {
                Title = item.TrackName ?? defaultTitle,
                Artist = item.ArtistName ?? defaultArtist,
                IsSynced = false,
                PlainLyrics = item.PlainLyrics,
                Source = "LRCLIB (Plain)"
            };
        }

        return SongLyrics.Empty();
    }

    private string GetCacheFilePath(string cacheKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)));
        return Path.Combine(_settingsService.GetLyricsCachePath(), $"{hash}.json");
    }

    private SongLyrics? TryGetFromCache(string cacheKey)
    {
        try
        {
            var path = GetCacheFilePath(cacheKey);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SongLyrics>(json);
            }
        }
        catch
        {
        }
        return null;
    }

    private void SaveToCache(string cacheKey, SongLyrics lyrics)
    {
        try
        {
            var path = GetCacheFilePath(cacheKey);
            var json = JsonSerializer.Serialize(lyrics);
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }
}
