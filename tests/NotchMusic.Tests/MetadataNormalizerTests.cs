using NotchMusic.Helpers;
using Xunit;

namespace NotchMusic.Tests;

public class MetadataNormalizerTests
{
    [Theory]
    [InlineData("Shape of You (Official Music Video)", "Ed Sheeran", "Shape of You", "Ed Sheeran")]
    [InlineData("Blinding Lights [MV]", "The Weeknd", "Blinding Lights", "The Weeknd")]
    [InlineData("Hotel California (Live)", "Eagles", "Hotel California (Live)", "Eagles")]
    [InlineData("Levitating (Acoustic)", "Dua Lipa", "Levitating (Acoustic)", "Dua Lipa")]
    [InlineData("Alone - Alan Walker", "", "Alan Walker", "Alone")]
    public void Normalize_HandlesYouTubeNoiseWhilePreservingVersions(
        string rawTitle, string rawArtist, string expectedTitle, string expectedArtist)
    {
        var (cleanTitle, cleanArtist) = MetadataNormalizer.Normalize(rawTitle, rawArtist);

        Assert.Equal(expectedTitle, cleanTitle);
        Assert.Equal(expectedArtist, cleanArtist);
    }

    [Fact]
    public void CreateCacheKey_ProducesConsistentKey()
    {
        var key1 = MetadataNormalizer.CreateCacheKey("Song Title (Official Video)", "Artist", TimeSpan.FromSeconds(180));
        var key2 = MetadataNormalizer.CreateCacheKey("Song Title", "Artist", TimeSpan.FromSeconds(180.4));

        Assert.Equal(key1, key2);
    }
}
