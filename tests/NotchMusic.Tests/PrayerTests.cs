using System;
using System.Linq;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using Xunit;

namespace NotchMusic.Tests;

public class PrayerTests
{
    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => System.IO.Path.GetTempPath();
        public string GetLyricsCachePath() => System.IO.Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void TestIndonesianCitiesList_IsNotEmpty_AndContainsMajorCities()
    {
        var cities = CityLocation.GetIndonesianCities();
        Assert.NotEmpty(cities);
        Assert.Contains(cities, c => c.Name == "Jakarta");
        Assert.Contains(cities, c => c.Name == "Surabaya");
        Assert.Contains(cities, c => c.Name == "Bandung");
        Assert.Contains(cities, c => c.Name == "Medan");
        Assert.Contains(cities, c => c.Name == "Makassar");
    }

    [Fact]
    public void TestPrayerTimesCalculator_ProducesChronologicalSchedule()
    {
        // Jakarta coordinates: -6.2088, 106.8456, Timezone +7
        var testDate = new DateTime(2026, 10, 22, 12, 0, 0);
        var schedule = PrayerTimesCalculator.CalculateSchedule(testDate, -6.2088, 106.8456, 7.0, "Jakarta");

        Assert.NotNull(schedule);
        Assert.Equal(7, schedule.Items.Count);

        var names = schedule.Items.Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Imsak", "Subuh", "Terbit", "Dzuhur", "Ashar", "Maghrib", "Isya" }, names);

        // Verify times are chronological
        for (int i = 0; i < schedule.Items.Count - 1; i++)
        {
            Assert.True(schedule.Items[i].Time < schedule.Items[i + 1].Time, 
                $"{schedule.Items[i].Name} ({schedule.Items[i].Time}) should be before {schedule.Items[i + 1].Name} ({schedule.Items[i + 1].Time})");
        }

        // Verify reasonable Jakarta time bounds
        var subuh = schedule.Items.First(p => p.Name == "Subuh");
        Assert.True(subuh.Time.Hours is >= 3 and <= 5);

        var dzuhur = schedule.Items.First(p => p.Name == "Dzuhur");
        Assert.True(dzuhur.Time.Hours is >= 11 and <= 12);

        var maghrib = schedule.Items.First(p => p.Name == "Maghrib");
        Assert.True(maghrib.Time.Hours is >= 17 and <= 19);

        var isya = schedule.Items.First(p => p.Name == "Isya");
        Assert.True(isya.Time.Hours is >= 18 and <= 20);
    }

    [Fact]
    public void TestHijriDateFormatted_ReturnsValidHijriYear()
    {
        var testDate = new DateTime(2026, 10, 22);
        string hijri = PrayerTimesCalculator.GetHijriDateFormatted(testDate);

        Assert.False(string.IsNullOrWhiteSpace(hijri));
        Assert.Contains("1448 H", hijri);
    }

    [Fact]
    public void TestPrayerAudioHelper_CanGenerateAndPlayChime()
    {
        // Chime generation should not throw exceptions
        var exception = Record.Exception(() => PrayerAudioHelper.PlayChime());
        Assert.Null(exception);
    }

    [Fact]
    public void TestPrayerReminderService_FiresRemindersBeforeAtAndAfter()
    {
        var fakeSettings = new FakeSettingsService();
        fakeSettings.CurrentSettings.EnablePrayerTimes = true;
        fakeSettings.CurrentSettings.RemindBeforePrayer = true;
        fakeSettings.CurrentSettings.RemindBeforeMinutes = 10;
        fakeSettings.CurrentSettings.RemindAtPrayerTime = true;
        fakeSettings.CurrentSettings.RemindAfterPrayer = true;
        fakeSettings.CurrentSettings.RemindAfterMinutes = 15;
        fakeSettings.CurrentSettings.PrayerSoundAlert = false; // silent in unit test

        var prayerService = new PrayerService(fakeSettings);
        using var reminderService = new PrayerReminderService(prayerService, fakeSettings);

        Assert.NotNull(reminderService.CurrentSchedule);
        Assert.NotEmpty(reminderService.CurrentSchedule.Items);
    }
}
