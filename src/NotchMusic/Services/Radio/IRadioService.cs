using NotchMusic.Models;

namespace NotchMusic.Services.Radio;

public interface IRadioService : IDisposable
{
    IReadOnlyList<RadioStation> Stations { get; }
    RadioStation? CurrentStation { get; }
    RadioPlaybackState PlaybackState { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    string StatusMessage { get; }

    event Action? StateChanged;
    event Action<RadioStation?>? StationChanged;

    void PlayStation(RadioStation station);
    void TogglePlayPause();
    void Stop();
    void NextStation(IEnumerable<RadioStation>? currentPool = null);
    void PreviousStation(IEnumerable<RadioStation>? currentPool = null);
    void ToggleFavorite(RadioStation station);
    RadioStation AddCustomStation(string name, string streamUrl, string category = "Custom", string cityOrCountry = "Custom Stream");
    void RemoveCustomStation(RadioStation station);
    Task<List<RadioStation>> SearchOnlineStationsAsync(string query, CancellationToken cancellationToken = default);
}
