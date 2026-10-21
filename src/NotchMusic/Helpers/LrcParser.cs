using System.Text.RegularExpressions;
using NotchMusic.Models;

namespace NotchMusic.Helpers;

public static class LrcParser
{
    // Matches [mm:ss.xx] or [mm:ss.xxx] or [mm:ss]
    private static readonly Regex TimeTagRegex = new(@"\[(\d{1,3}):(\d{2})(?:\.(\d{1,3}))?\]", RegexOptions.Compiled);
    // Matches metadata like [offset:+500] or [ti:Title]
    private static readonly Regex MetadataRegex = new(@"^\[([a-zA-Z]+):([^\]]*)\]", RegexOptions.Compiled);

    public static SongLyrics Parse(string lrcContent, TimeSpan manualOffset = default)
    {
        if (string.IsNullOrWhiteSpace(lrcContent))
        {
            return SongLyrics.Empty();
        }

        var lines = lrcContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var parsedLines = new List<LyricLine>();
        var title = string.Empty;
        var artist = string.Empty;
        var tagOffsetMs = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Check if line is pure metadata tag
            var metaMatch = MetadataRegex.Match(line);
            if (metaMatch.Success && !TimeTagRegex.IsMatch(line))
            {
                var tag = metaMatch.Groups[1].Value.ToLowerInvariant();
                var val = metaMatch.Groups[2].Value.Trim();
                if (tag == "ti") title = val;
                else if (tag == "ar") artist = val;
                else if (tag == "offset" && int.TryParse(val, out var offsetVal))
                {
                    tagOffsetMs = offsetVal;
                }
                continue;
            }

            // Extract all time tags in this line
            var matches = TimeTagRegex.Matches(line);
            if (matches.Count > 0)
            {
                // Strip all time tags to get text
                var text = TimeTagRegex.Replace(line, "").Trim();

                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[1].Value, out var minutes) &&
                        int.TryParse(match.Groups[2].Value, out var seconds))
                    {
                        var millis = 0;
                        if (match.Groups[3].Success)
                        {
                            var fracStr = match.Groups[3].Value;
                            if (fracStr.Length == 1) millis = int.Parse(fracStr) * 100;
                            else if (fracStr.Length == 2) millis = int.Parse(fracStr) * 10;
                            else if (fracStr.Length >= 3) millis = int.Parse(fracStr.Substring(0, 3));
                        }

                        var ts = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(millis);
                        parsedLines.Add(new LyricLine(ts, text));
                    }
                }
            }
        }

        if (parsedLines.Count == 0)
        {
            // Fallback: treated as plain text
            return new SongLyrics
            {
                Title = title,
                Artist = artist,
                IsSynced = false,
                PlainLyrics = lrcContent.Trim(),
                Source = "Plain text / Unsynced LRC"
            };
        }

        // Apply metadata tag offset (in milliseconds: positive means lyrics delayed)
        var totalOffset = TimeSpan.FromMilliseconds(tagOffsetMs) + manualOffset;
        if (totalOffset != TimeSpan.Zero)
        {
            foreach (var item in parsedLines)
            {
                var adjusted = item.Timestamp + totalOffset;
                item.Timestamp = adjusted < TimeSpan.Zero ? TimeSpan.Zero : adjusted;
            }
        }

        // Sort lines by timestamp in case they were not in order
        var sortedLines = parsedLines.OrderBy(l => l.Timestamp).ToList();

        return new SongLyrics
        {
            Title = title,
            Artist = artist,
            IsSynced = true,
            Lines = sortedLines,
            Offset = totalOffset,
            Source = "Synced LRC"
        };
    }

    public static int GetActiveLineIndex(IReadOnlyList<LyricLine> lines, TimeSpan currentPosition)
    {
        if (lines == null || lines.Count == 0) return -1;
        if (currentPosition < lines[0].Timestamp) return -1;

        int low = 0;
        int high = lines.Count - 1;
        int result = 0;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (lines[mid].Timestamp <= currentPosition)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }
}
