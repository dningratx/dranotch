using NotchMusic.Models;

namespace NotchMusic.Services.LiveTv;

public interface ILiveTvService : IDisposable
{
    IReadOnlyList<LiveTvChannel> Channels { get; }
    IReadOnlyList<LiveTvCountryOption> Countries { get; }
    LiveTvChannel? CurrentChannel { get; }
    LiveTvPlaybackState PlaybackState { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    string StatusMessage { get; }
    bool IsLoadingCatalog { get; }

    event Action? StateChanged;
    event Action<LiveTvChannel?>? ChannelChanged;
    event Action<LiveTvChannel, string>? PlayStreamRequested;
    event Action? TogglePlayPauseRequested;
    event Action? StopStreamRequested;
    event Action<double, bool>? VolumeChanged;
    event Action? CatalogUpdated;

    void PlayChannel(LiveTvChannel channel, int sourceIndex = 0);
    void CycleNextSource();
    bool TryNextFallbackSource();
    void TogglePlayPause();
    void Stop();
    void NextChannel(IEnumerable<LiveTvChannel>? currentPool = null);
    void PreviousChannel(IEnumerable<LiveTvChannel>? currentPool = null);
    void ToggleFavorite(LiveTvChannel channel);
    LiveTvChannel AddCustomChannel(string name, string streamUrl, string category = "Custom", string countryCode = "id", string countryName = "Custom Stream");
    void RemoveCustomChannel(LiveTvChannel channel);

    void NotifyPlaybackPlaying();
    void NotifyPlaybackPaused();
    void NotifyPlaybackBuffering();
    void NotifyPlaybackError(string? reason = null);

    Task<List<LiveTvChannel>> LoadCountryChannelsAsync(string countryCode, CancellationToken cancellationToken = default);
    Task<List<LiveTvChannel>> LoadCategoryChannelsAsync(string categoryFilter, string? preferredCountryCode = null, CancellationToken cancellationToken = default);
    Task<List<LiveTvChannel>> SearchOnlineChannelsAsync(string query, string? countryCode = null, CancellationToken cancellationToken = default);
}
