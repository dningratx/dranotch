using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Models;
using NotchMusic.Services.LiveTv;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

[Collection("WpfWindowTests")]
public class LiveTvTests
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
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => System.IO.Path.GetTempPath();
        public string GetLyricsCachePath() => System.IO.Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void LiveTvService_BuiltInChannels_ContainsIndonesianAndInternationalChannels()
    {
        var settings = new FakeSettingsService();
        using var liveTvService = new LiveTvService(settings);

        Assert.NotEmpty(liveTvService.Channels);
        Assert.NotEmpty(liveTvService.Countries);
        Assert.Contains(liveTvService.Channels, c => c.CountryCode == "id");
        Assert.Contains(liveTvService.Channels, c => c.Category == "Berita");
        Assert.Contains(liveTvService.Channels, c => c.Category == "Religi");
        Assert.Contains(liveTvService.Channels, c => c.Category == "Hiburan");
        Assert.NotNull(liveTvService.CurrentChannel);
    }

    [Fact]
    public void LiveTvService_PlayStopNextPrevious_FallbackAndFavorites_WorkCorrectly()
    {
        var settings = new FakeSettingsService();
        using var liveTvService = new LiveTvService(settings);

        var first = liveTvService.Channels[0];
        var second = liveTvService.Channels[1];

        string? requestedStreamUrl = null;
        liveTvService.PlayStreamRequested += (_, url) => requestedStreamUrl = url;

        liveTvService.PlayChannel(first);
        Assert.Equal(first, liveTvService.CurrentChannel);
        Assert.True(first.IsCurrent);
        Assert.Equal(first.StreamUrl, requestedStreamUrl);
        Assert.Equal(LiveTvPlaybackState.Buffering, liveTvService.PlaybackState);

        liveTvService.NotifyPlaybackPlaying();
        Assert.Equal(LiveTvPlaybackState.Playing, liveTvService.PlaybackState);
        Assert.True(first.IsPlaying);

        // Fallback source cycling on TVRI Nasional (which has multiple sources)
        Assert.True(first.SourceCount > 1);
        liveTvService.CycleNextSource();
        Assert.Equal(1, first.ActiveSourceIndex);

        liveTvService.NextChannel();
        Assert.Equal(second, liveTvService.CurrentChannel);
        Assert.True(second.IsCurrent);

        liveTvService.PreviousChannel();
        Assert.Equal(first, liveTvService.CurrentChannel);

        // Favorite toggle
        Assert.False(first.IsFavorite);
        liveTvService.ToggleFavorite(first);
        Assert.True(first.IsFavorite);
        Assert.Contains(first.Id, settings.CurrentSettings.FavoriteLiveTvChannelIds);

        liveTvService.Stop();
        Assert.Equal(LiveTvPlaybackState.Stopped, liveTvService.PlaybackState);
    }

    [Fact]
    public void MainViewModel_LiveTvFilterAndCustomChannel_WorkCorrectly()
    {
        var settings = new FakeSettingsService();
        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settings);

        int initialCount = vm.LiveTvChannels.Count;
        Assert.True(initialCount > 0);

        // Filter by Berita
        vm.SetLiveTvCategoryCommand.Execute("Berita");
        var filtered = vm.FilteredLiveTvChannels.Cast<LiveTvChannel>().ToList();
        Assert.NotEmpty(filtered);
        Assert.All(filtered, c => Assert.Equal("Berita", c.Category));

        // Add custom channel (YouTube watch URL normalized automatically)
        vm.CustomLiveTvName = "TV Custom Nusantara";
        vm.CustomLiveTvUrl = "https://www.youtube.com/watch?v=DOOrIxw5xOw";
        vm.SaveCustomLiveTvCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.LiveTvChannels.Count);
        var added = vm.LiveTvChannels.FirstOrDefault(c => c.Name == "TV Custom Nusantara");
        Assert.NotNull(added);
        Assert.True(added.IsCustom);
        Assert.True(added.IsYouTubeStream);

        // Delete custom channel
        vm.DeleteCustomLiveTvChannelCommand.Execute(added);
        Assert.Equal(initialCount, vm.LiveTvChannels.Count);
    }

    [Fact]
    public void LiveTvWindow_InitializesWithoutErrors()
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
                var window = new Views.LiveTvWindow(mainVm, settings);
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
