using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Reminder;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

[Collection("WpfWindowTests")]
public class ReminderTests
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
        public event EventHandler? PlaybackStartedOnSpotify;
        public event EventHandler? PlaybackStartedOnYouTubeMusic;
        public Task PauseSessionByKeywordAsync(string keyword) => Task.CompletedTask;
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
        private readonly string _tempDir;

        public FakeSettingsService()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "NotchMusicReminderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public AppSettings CurrentSettings { get; } = new()
        {
            EnableReminders = true,
            ReminderSoundAlert = false
        };

        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => _tempDir;
        public string GetLyricsCachePath() => _tempDir;
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void TestIntervalReminder_CalculatesNextTriggerAndSummary()
    {
        var now = new DateTime(2026, 11, 4, 10, 0, 0);
        var item = new ReminderItem
        {
            Title = "Minum Air Putih",
            Icon = "💧",
            Type = ReminderType.Interval,
            IntervalMinutes = 60,
            IsEnabled = true
        };

        item.CalculateNextTrigger(now);
        Assert.NotNull(item.NextTriggerTime);
        Assert.Equal(now.AddMinutes(60), item.NextTriggerTime.Value);

        item.RefreshDynamicProperties();
        Assert.Equal("Setiap 1 jam sekali", item.ScheduleSummaryText);
        Assert.Equal("#38BDF8", item.AccentColor);
    }

    [Fact]
    public void TestDailyTimesReminder_PicksNextUpcomingTimeOrTomorrow()
    {
        var item = new ReminderItem
        {
            Title = "Minum Obat & Vitamin",
            Icon = "💊",
            Type = ReminderType.DailyTime,
            DailyTimes = new List<string> { "08:00", "13:00", "20:00" },
            IsEnabled = true
        };

        // At 09:30 -> next should be 13:00 today
        var morning = new DateTime(2026, 11, 4, 9, 30, 0);
        item.CalculateNextTrigger(morning, forceAdvance: true);
        Assert.NotNull(item.NextTriggerTime);
        Assert.Equal(new DateTime(2026, 11, 4, 13, 0, 0), item.NextTriggerTime.Value);

        // At 21:00 -> next should be 08:00 tomorrow
        var night = new DateTime(2026, 11, 4, 21, 0, 0);
        item.CalculateNextTrigger(night, forceAdvance: true);
        Assert.NotNull(item.NextTriggerTime);
        Assert.Equal(new DateTime(2026, 11, 5, 8, 0, 0), item.NextTriggerTime.Value);
    }

    [Fact]
    public void TestReminderService_SeedsDefaultsAndSupportsCrudAndSnooze()
    {
        var settings = new FakeSettingsService();
        using var service = new ReminderService(settings);

        // Default seeded reminders should exist
        Assert.NotEmpty(service.Reminders);
        Assert.Contains(service.Reminders, r => r.Title.Contains("Minum Air"));
        Assert.Contains(service.Reminders, r => r.Title.Contains("Minum Obat"));

        int initialCount = service.Reminders.Count;

        // Add custom reminder
        var added = service.AddReminder(new ReminderItem
        {
            Title = "Minum Vitamin C",
            Notes = "Sehabis makan siang",
            Icon = "💊",
            Category = "Obat & Suplemen",
            Type = ReminderType.DailyTime,
            DailyTimes = new List<string> { "12:30" }
        });

        Assert.Equal(initialCount + 1, service.Reminders.Count);
        Assert.Equal("Minum Vitamin C", added.Title);

        // Mark completed occurrence
        service.CompleteReminderOccurrence(added.Id);
        Assert.Equal(1, added.CompletedTodayCount);
        Assert.True(added.HasCompletedToday);

        // Snooze reminder
        service.SnoozeReminder(added.Id, 10);
        Assert.NotNull(added.SnoozedUntil);
        Assert.True(added.SnoozedUntil > DateTime.Now.AddMinutes(8));
        Assert.True(added.IsSnoozed);

        // Add from preset
        var presetItem = service.AddFromPreset("water_30m");
        Assert.NotNull(presetItem);
        Assert.Equal(initialCount + 2, service.Reminders.Count);

        // Delete reminders
        service.DeleteReminder(added.Id);
        service.DeleteReminder(presetItem.Id);
        Assert.Equal(initialCount, service.Reminders.Count);
    }

    [Fact]
    public void ReminderWindow_InitializesWithoutErrors()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new FakeSettingsService();
                var mainVm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settings);
                var window = new Views.ReminderWindow(mainVm, settings);
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

    [Fact]
    public void MainViewModel_ActiveReminders_ContainsAllEnabledRemindersWithIconsAndCountdowns()
    {
        var settings = new FakeSettingsService();
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settings);

        Assert.NotEmpty(mainVm.ActiveReminders);
        Assert.All(mainVm.ActiveReminders, r =>
        {
            Assert.True(r.IsEnabled);
            Assert.False(string.IsNullOrWhiteSpace(r.Icon));
            Assert.False(string.IsNullOrWhiteSpace(r.CountdownText));
        });

        int initialActive = mainVm.ActiveReminders.Count;
        var first = mainVm.ActiveReminders.First();

        // Toggle off -> removed from ActiveReminders
        mainVm.ToggleReminderItemCommand.Execute(first);
        Assert.Equal(initialActive - 1, mainVm.ActiveReminders.Count);
        Assert.DoesNotContain(first, mainVm.ActiveReminders);

        // Toggle back on -> added back to ActiveReminders
        mainVm.ToggleReminderItemCommand.Execute(first);
        Assert.Equal(initialActive, mainVm.ActiveReminders.Count);
        Assert.Contains(first, mainVm.ActiveReminders);
    }
}
