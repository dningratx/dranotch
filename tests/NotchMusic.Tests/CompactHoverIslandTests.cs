using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Update;
using NotchMusic.ViewModels;
using Xunit;

namespace NotchMusic.Tests;

#pragma warning disable CS0067

public class CompactHoverIslandTests
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
        public void FireTrackChanged(TrackInfo? track) => TrackChanged?.Invoke(this, track);
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
        public AppSettings CurrentSettings { get; } = new AppSettings();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => "appdata";
        public string GetLyricsCachePath() => "cache";
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void CompactHoverIsland_WhenIdle_ShowsOnlyLogo_IconsHidden()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;

        // When idle (not hovering, not pinned), ShowComponentIcons should be false and ShowIdlePrayerBesideLogo true by default
        Assert.False(vm.ShowComponentIcons);
        Assert.False(vm.ShowMusicInNotch);
        Assert.True(vm.ShowIdlePrayerBesideLogo);
        Assert.False(vm.IsHovering);
    }

    [Fact]
    public void CompactHoverIsland_WhenHovered_Shows5ComponentIcons()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;

        // Hover over the notch / logo
        vm.OnMouseEnter();

        // 5 component icons should become visible
        Assert.True(vm.ShowComponentIcons);
        Assert.True(vm.IsHovering);
    }

    [Fact]
    public void CompactHoverIsland_WhenPinned_KeepsComponentIconsVisible()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;

        vm.IsPinned = true;

        // Even without hovering, pinned state keeps icons visible
        Assert.True(vm.ShowComponentIcons);
    }

    [Fact]
    public void CompactHoverIsland_TooltipsAndCommands_AreConfigured()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;

        Assert.NotNull(vm.OpenYouTubeMusicCommand);
        Assert.NotNull(vm.OpenRadioCommand);
        Assert.NotNull(vm.OpenTodoCommand);
        Assert.NotNull(vm.OpenFloatingBrowserCommand);
        Assert.NotNull(vm.OpenPrayerScheduleCommand);

        Assert.False(string.IsNullOrEmpty(vm.MusicNotchTooltip));
        Assert.False(string.IsNullOrEmpty(vm.RadioNotchTooltip));
        Assert.False(string.IsNullOrEmpty(vm.TodoNotchTooltip));
        Assert.False(string.IsNullOrEmpty(vm.PrayerScheduleTooltip));
        Assert.StartsWith("v", vm.VersionButtonText);
        Assert.False(string.IsNullOrEmpty(vm.VersionButtonTooltip));
    }

    [Fact]
    public void SetModeCommand_Expanded_OpensMusicWithLyricsAndClosesPrayerPanel()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;
        vm.IsPrayerPanelOpen = true;

        vm.SetModeCommand.Execute("Expanded");

        Assert.Equal(PanelMode.Expanded, vm.CurrentMode);
        Assert.False(vm.IsPrayerPanelOpen);
        Assert.True(vm.ShowNormalExpandedContent);
    }

    private class FakeUpdateService : IUpdateService
    {
        public string CurrentVersion => "1.5.0";
        public UpdateInfo? UpdateToReturn { get; set; }
        public bool CheckCalled { get; private set; }
        public bool DownloadCalled { get; private set; }
        public string? InstalledPath { get; private set; }

        public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            CheckCalled = true;
            return Task.FromResult(UpdateToReturn);
        }

        public Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            DownloadCalled = true;
            progress?.Report(50);
            progress?.Report(100);
            return Task.FromResult(@"C:\temp\DRANOTCH-Setup.exe");
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            InstalledPath = installerPath;
        }
    }

    [Fact]
    public async Task VersionButtonClick_WhenUpdateAvailable_ChecksDownloadsAndInstallsDirectly()
    {
        var fakeUpdate = new FakeUpdateService
        {
            UpdateToReturn = new UpdateInfo
            {
                Version = "1.6.0",
                IsNewerVersion = true,
                DownloadUrl = "https://example.com/DRANOTCH-Setup.exe"
            }
        };

        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            new FakeSettingsService(),
            updateService: fakeUpdate);

        await vm.CheckAndInstallUpdateAsync();

        Assert.True(fakeUpdate.CheckCalled);
        Assert.True(fakeUpdate.DownloadCalled);
        Assert.Equal(@"C:\temp\DRANOTCH-Setup.exe", fakeUpdate.InstalledPath);
    }

    [Fact]
    public void OpenAndCloseMusicWindowCommand_DetachesMusicWhileKeepingMainNotchCompact()
    {
        var vm = new MainViewModel(new FakeMediaSessionService(), new FakeLyricsService(), new FakeSettingsService());
        vm.CurrentMode = PanelMode.Compact;
        bool openMusicEventFired = false;
        vm.RequestOpenMusic += () => openMusicEventFired = true;

        vm.OpenMusicWindowCommand.Execute(null);

        Assert.True(vm.IsMusicWindowOpen);
        Assert.True(openMusicEventFired);
        Assert.Equal(PanelMode.Compact, vm.CurrentMode);

        vm.CloseMusicWindowCommand.Execute(null);

        Assert.False(vm.IsMusicWindowOpen);
        Assert.Equal(PanelMode.Compact, vm.CurrentMode);

        // Verify To-Do List detaches and switches notch to icon-only mode (IsTodoDetachedWindowVisible = true)
        Assert.False(vm.IsTodoDetachedWindowVisible);
        vm.OpenTodoCommand.Execute(null);
        Assert.True(vm.IsTodoDetachedWindowVisible);
        Assert.Equal(PanelMode.Compact, vm.CurrentMode);

        // Verify AttachTodoCommand merges To-Do List back into full notch pill (IsTodoDetachedWindowVisible = false)
        vm.AttachTodoCommand.Execute(null);
        Assert.False(vm.IsTodoDetachedWindowVisible);
        Assert.Equal(PanelMode.Compact, vm.CurrentMode);

        // Verify clicking DRANOTCH logo button invokes RequestOpenAbout
        bool openAboutFired = false;
        vm.RequestOpenAbout += () => openAboutFired = true;
        vm.OpenAboutCommand.Execute(null);
        Assert.True(openAboutFired);

        // Verify after Isya (e.g. 22:40), Subuh countdown is for tomorrow's Subuh ("... lagi", not "... lalu")
        var lateNight = new DateTime(2026, 10, 24, 22, 40, 0);
        var schedule = NotchMusic.Services.Prayer.PrayerTimesCalculator.CalculateSchedule(lateNight, -6.2088, 106.8456, 7.0, "Jakarta");
        Assert.NotNull(schedule.NextPrayer);
        Assert.Equal("Subuh", schedule.NextPrayer!.Name);
        Assert.EndsWith("lagi", schedule.NextPrayer.CountdownText);
        Assert.True(schedule.TimeUntilNextPrayer.TotalHours > 4 && schedule.TimeUntilNextPrayer.TotalHours < 8);

        // Verify when music is playing while idle, ShowMusicInNotch and ShowMusicPillInNotch are true
        var fakeMedia = new FakeMediaSessionService();
        var vmIdleMusic = new MainViewModel(fakeMedia, new FakeLyricsService(), new FakeSettingsService());
        vmIdleMusic.CurrentMode = PanelMode.Compact;
        Assert.False(vmIdleMusic.ShowComponentIcons);
        Assert.False(vmIdleMusic.ShowMusicInNotch);

        fakeMedia.FireTrackChanged(new TrackInfo { Title = "Test Song", Artist = "Test Artist", IsPlaying = true });
        Assert.False(vmIdleMusic.ShowComponentIcons);
        Assert.True(vmIdleMusic.IsPlaying);
        Assert.True(vmIdleMusic.ShowMusicInNotch);
        Assert.True(vmIdleMusic.ShowMusicPillInNotch);

        // Verify ToggleBezelMinimizeCommand minimizes the idle notch into the top bezel and restores it
        Assert.False(vmIdleMusic.IsBezelMinimized);
        Assert.True(vmIdleMusic.IsNotBezelMinimized);
        Assert.True(vmIdleMusic.ShowCompactView);

        vmIdleMusic.ToggleBezelMinimizeCommand.Execute(null);
        Assert.True(vmIdleMusic.IsBezelMinimized);
        Assert.False(vmIdleMusic.IsNotBezelMinimized);
        Assert.False(vmIdleMusic.ShowCompactView);
        Assert.False(vmIdleMusic.ShowMusicInNotch);
        Assert.False(vmIdleMusic.ShowIdlePrayerBesideLogo);

        // Even when hovered while minimized to bezel, component icons stay hidden until restored
        vmIdleMusic.OnMouseEnter();
        Assert.False(vmIdleMusic.ShowComponentIcons);

        // Clicking the bezel arrow button again restores the notch
        vmIdleMusic.ToggleBezelMinimizeCommand.Execute(null);
        Assert.False(vmIdleMusic.IsBezelMinimized);
        Assert.True(vmIdleMusic.ShowCompactView);
        Assert.True(vmIdleMusic.ShowComponentIcons);
    }
}
