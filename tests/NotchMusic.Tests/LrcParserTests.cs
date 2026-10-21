using NotchMusic.Helpers;
using NotchMusic.Models;
using Xunit;

namespace NotchMusic.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parse_StandardLrc_ReturnsOrderedLines()
    {
        var lrc = @"
[ti:Never Gonna Give You Up]
[ar:Rick Astley]
[00:18.50]We're no strangers to love
[00:22.80]You know the rules and so do I
[00:27.10]A full commitment's what I'm thinking of
";

        var result = LrcParser.Parse(lrc);

        Assert.True(result.IsSynced);
        Assert.Equal("Never Gonna Give You Up", result.Title);
        Assert.Equal("Rick Astley", result.Artist);
        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(18.5), result.Lines[0].Timestamp);
        Assert.Equal("We're no strangers to love", result.Lines[0].Text);
    }

    [Fact]
    public void Parse_MultipleTimestampsOnSingleLine_ExpandsAndSortsCorrectly()
    {
        var lrc = @"
[00:12.00][00:24.00]Chorus line repeated
[00:05.00]First intro
";

        var result = LrcParser.Parse(lrc);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Lines[0].Timestamp);
        Assert.Equal("First intro", result.Lines[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Lines[1].Timestamp);
        Assert.Equal("Chorus line repeated", result.Lines[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(24), result.Lines[2].Timestamp);
        Assert.Equal("Chorus line repeated", result.Lines[2].Text);
    }

    [Fact]
    public void Parse_LrcOffsetTag_AppliesOffset()
    {
        // [offset:+500] shifts 500ms forward
        var lrc = @"
[offset:+500]
[00:10.00]Line with offset
";

        var result = LrcParser.Parse(lrc);

        Assert.Single(result.Lines);
        Assert.Equal(TimeSpan.FromSeconds(10.5), result.Lines[0].Timestamp);
    }

    [Fact]
    public void Parse_InvalidLines_DoNotCrash()
    {
        var lrc = @"
[invalid_tag]
Some random unformatted text
[999:999:999] bad timestamp
[00:03.00]Valid line
";

        var result = LrcParser.Parse(lrc);

        Assert.True(result.IsSynced);
        Assert.Single(result.Lines);
        Assert.Equal("Valid line", result.Lines[0].Text);
    }

    [Fact]
    public void GetActiveLineIndex_BinarySearch_PicksCorrectLine()
    {
        var lines = new List<LyricLine>
        {
            new(TimeSpan.FromSeconds(5), "Line 1"),
            new(TimeSpan.FromSeconds(10), "Line 2"),
            new(TimeSpan.FromSeconds(15), "Line 3")
        };

        Assert.Equal(-1, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(8)));
        Assert.Equal(1, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(10)));
        Assert.Equal(1, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(14.9)));
        Assert.Equal(2, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(15)));
        Assert.Equal(2, LrcParser.GetActiveLineIndex(lines, TimeSpan.FromSeconds(30)));
    }
}
