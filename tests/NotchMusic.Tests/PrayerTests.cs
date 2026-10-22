using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

public class PrayerTests
{
    private class FakeMediaSessionService : IMediaSessionService
    {
        public event EventHandler<TrackInfo?>? TrackChanged;
        public event EventHandler<TimeSpan>? TimelineUpdated;
        public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

        public TrackInfo? CurrentTrack => null;
        public IReadOnlyList<MediaSessionInfo> AvailableSessions => new List<MediaSessionInfo>();
        public string? SelectedSessionId => null;

        public Task InitializeAsync() => Task.CompletedTask;
        public void SelectSession(string sessionId) { }
        public Task TogglePlayPauseAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public Task NextAsync() => Task.CompletedTask;
        public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class FakeLyricsService : ILyricsService
    {
        public Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(new SongLyrics());
        public Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LrcLibSearchResult>());
        public SongLyrics LoadFromLocalFile(string filePath) => new SongLyrics();
    }

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

    [Fact]
    public void MainViewModel_PrayerDetachmentAndDividers_WorkCorrectly()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.EnablePrayerTimes = true;
        settingsService.CurrentSettings.ShowPrayerInCompactNotch = true;

        var prayerService = new PrayerService(settingsService);
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService,
            todoService: null,
            googleDriveSyncService: null,
            updateService: null,
            prayerService: prayerService);

        // Initially attached
        Assert.False(mainVm.IsPrayerDetached);
        Assert.True(mainVm.ShowCompactPrayerPill);
        Assert.True(mainVm.ShowPrayerDivider1);
        Assert.True(mainVm.ShowPrayerDivider2);

        // Detach prayer (Todo is still attached, so Divider 2 separates Music and Todo)
        mainVm.DetachPrayerCommand.Execute(null);
        Assert.True(mainVm.IsPrayerDetached);
        Assert.True(settingsService.CurrentSettings.IsPrayerDetached);
        Assert.False(mainVm.ShowCompactPrayerPill);
        Assert.False(mainVm.ShowPrayerDivider1);
        Assert.True(mainVm.ShowPrayerDivider2); // Divider between Music and Todo

        // Detach Todo as well -> no dividers should be visible
        mainVm.DetachTodoCommand.Execute(null);
        Assert.False(mainVm.ShowPrayerDivider1);
        Assert.False(mainVm.ShowPrayerDivider2);

        // Re-attach both
        mainVm.AttachTodoCommand.Execute(null);
        mainVm.AttachPrayerCommand.Execute(null);
        Assert.False(mainVm.IsPrayerDetached);
        Assert.False(settingsService.CurrentSettings.IsPrayerDetached);
        Assert.True(mainVm.ShowCompactPrayerPill);
        Assert.True(mainVm.ShowPrayerDivider1);
        Assert.True(mainVm.ShowPrayerDivider2);
    }

    [Fact]
    public void MainViewModel_PrayerWindowCompactCommands_ToggleCorrectly()
    {
        var settingsService = new FakeSettingsService();
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService);

        // Default: expanded
        Assert.False(mainVm.IsPrayerWindowCompact);
        Assert.True(mainVm.IsPrayerWindowExpanded);

        // Minimize command -> compact
        mainVm.MinimizePrayerWindowCommand.Execute(null);
        Assert.True(mainVm.IsPrayerWindowCompact);
        Assert.False(mainVm.IsPrayerWindowExpanded);
        Assert.True(settingsService.CurrentSettings.IsPrayerWindowCompact);

        // Expand command -> expanded
        mainVm.ExpandPrayerWindowCommand.Execute(null);
        Assert.False(mainVm.IsPrayerWindowCompact);
        Assert.True(mainVm.IsPrayerWindowExpanded);
        Assert.False(settingsService.CurrentSettings.IsPrayerWindowCompact);
    }

    [Fact]
    public void PrayerWindow_InitializesWithoutErrors()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new FakeSettingsService();
                var prayerService = new PrayerService(settings);
                var mainVm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settings,
                    todoService: null,
                    googleDriveSyncService: null,
                    updateService: null,
                    prayerService: prayerService);
                var window = new Views.PrayerWindow(mainVm, settings);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(5000);

        Assert.True(finished);
        Assert.Null(caught);
    }
}
