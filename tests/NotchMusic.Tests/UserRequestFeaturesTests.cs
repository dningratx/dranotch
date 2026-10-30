using System.Linq;
using NotchMusic.Helpers;
using NotchMusic.Models;
using Xunit;

namespace NotchMusic.Tests;

public class UserRequestFeaturesTests
{
    [Fact]
    public void YouTubeMusicSettings_DefaultSize_IsPhoneDimensions()
    {
        var settings = new AppSettings();
        // Sesuai permintaan user: default seukuran handphone
        Assert.Equal(380, settings.YouTubeMusicWidth);
        Assert.Equal(740, settings.YouTubeMusicHeight);
    }

    [Fact]
    public void TodoCategoryExpansionHelper_ToggleAndCollapse_WorksCorrectly()
    {
        TodoCategoryExpansionHelper.ExpandAll();
        Assert.False(TodoCategoryExpansionHelper.IsCollapsed("TKIKHLAS"));
        Assert.False(TodoCategoryExpansionHelper.IsCollapsed("KOPERASI"));

        // Toggle TKIKHLAS to collapsed
        TodoCategoryExpansionHelper.Toggle("TKIKHLAS");
        Assert.True(TodoCategoryExpansionHelper.IsCollapsed("TKIKHLAS"));
        Assert.False(TodoCategoryExpansionHelper.IsCollapsed("KOPERASI"));

        // Toggle again to expand
        TodoCategoryExpansionHelper.Toggle("TKIKHLAS");
        Assert.False(TodoCategoryExpansionHelper.IsCollapsed("TKIKHLAS"));

        // Collapse all
        var categories = new[] { "TKIKHLAS", "KOPERASI", "NAMOO", "RTS", "DRALOGY" };
        TodoCategoryExpansionHelper.CollapseAll(categories);
        foreach (var cat in categories)
        {
            Assert.True(TodoCategoryExpansionHelper.IsCollapsed(cat));
        }

        // Expand all
        TodoCategoryExpansionHelper.ExpandAll();
        foreach (var cat in categories)
        {
            Assert.False(TodoCategoryExpansionHelper.IsCollapsed(cat));
        }
    }

    [Fact]
    public void PrayerAudioHelper_AvailableSounds_ContainsTopAdhans()
    {
        var sounds = PrayerAudioHelper.AvailableSounds;
        Assert.NotNull(sounds);
        Assert.True(sounds.Count >= 9);

        // Pastikan pilihan-pilihan adzan terbaik tersedia
        Assert.Contains(sounds, s => s.Id == "makkah");
        Assert.Contains(sounds, s => s.Id == "madinah");
        Assert.Contains(sounds, s => s.Id == "alaqsa");
        Assert.Contains(sounds, s => s.Id == "alafasy");
        Assert.Contains(sounds, s => s.Id == "abdulbasit");
        Assert.Contains(sounds, s => s.Id == "mansoor");
        Assert.Contains(sounds, s => s.Id == "refaat");
        Assert.Contains(sounds, s => s.Id == "turkey");
        Assert.Contains(sounds, s => s.Id == "fajr");
        Assert.Contains(sounds, s => s.Id == "chime");
        Assert.Contains(sounds, s => s.Id == "custom");
    }

    [Fact]
    public void AppSettings_AdhanSoundDefaults_AreConfigured()
    {
        var settings = new AppSettings();
        Assert.Equal("makkah", settings.SelectedAdhanSound);
        Assert.Equal(0.85, settings.AdhanVolume);
        Assert.False(settings.AdhanCustomDurationEnabled);
        Assert.Equal(0, settings.AdhanStartMinutes);
        Assert.Equal(0, settings.AdhanStartSeconds);
        Assert.Equal(1, settings.AdhanEndMinutes);
        Assert.Equal(0, settings.AdhanEndSeconds);
    }

    [Fact]
    public void AppSettings_AdhanCustomDuration_CalculatesSecondsCorrectly()
    {
        var settings = new AppSettings
        {
            AdhanCustomDurationEnabled = true,
            AdhanStartMinutes = 0,
            AdhanStartSeconds = 45,
            AdhanEndMinutes = 2,
            AdhanEndSeconds = 15
        };

        Assert.Equal(45, settings.GetAdhanStartTotalSeconds());
        Assert.Equal(135, settings.GetAdhanEndTotalSeconds()); // 2*60 + 15 = 135

        // When disabled, returns 0 so it plays full audio
        settings.AdhanCustomDurationEnabled = false;
        Assert.Equal(0, settings.GetAdhanStartTotalSeconds());
        Assert.Equal(0, settings.GetAdhanEndTotalSeconds());
    }

    [Fact]
    public void PrayerAudioHelper_PlaybackControlProperties_ExistAndDefaultCorrectly()
    {
        Assert.False(PrayerAudioHelper.IsPlaying);
        Assert.False(PrayerAudioHelper.IsPaused);
        Assert.Equal(0, PrayerAudioHelper.CurrentPositionSeconds);
    }
}
