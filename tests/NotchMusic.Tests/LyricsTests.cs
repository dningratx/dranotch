using NotchMusic.Helpers;
using NotchMusic.Models;
using Xunit;

namespace NotchMusic.Tests;

public class LyricsTests
{
    [Fact]
    public void LrcParser_PlainLyricsFallback_SetsIsSyncedFalse()
    {
        var plainText = "Guess what baby\nThe things we say came true today\n'Cause we listened to our hearts";
        var result = LrcParser.Parse(plainText);

        Assert.False(result.IsSynced);
        Assert.Equal(plainText.Trim(), result.PlainLyrics.Trim());
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void LrcParser_SyncedLyrics_SetsIsSyncedTrue()
    {
        var syncedText = "[00:00.69] Guess what, baby\n[00:07.75] Came true today";
        var result = LrcParser.Parse(syncedText);

        Assert.True(result.IsSynced);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("Guess what, baby", result.Lines[0].Text);
    }
}
