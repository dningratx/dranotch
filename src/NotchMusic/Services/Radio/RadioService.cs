using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.Media.Core;
using Windows.Media.Playback;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Radio;

public class RadioService : IRadioService
{
    private readonly ISettingsService _settingsService;
    private readonly List<RadioStation> _stations = new();
    private readonly HttpClient _httpClient;
    private readonly object _playerLock = new();
    private MediaPlayer? _mediaPlayer;
    private MediaSource? _currentMediaSource;
    private CancellationTokenSource? _connectTimeoutCts;
    private RadioStation? _currentStation;
    private RadioPlaybackState _playbackState = RadioPlaybackState.Stopped;
    private string _statusMessage = "Siap memutar radio";
    private int _currentFallbackIndex = -1;
    private bool _triedOnlineResolve;
    private bool _hasCurrentAttemptOpened;
    private int _playAttemptVersion;
    private readonly HashSet<string> _triedUrlsInCurrentSession = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    private static readonly string[] RadioBrowserMirrors =
    [
        "https://de1.api.radio-browser.info",
        "https://fi1.api.radio-browser.info",
        "https://at1.api.radio-browser.info",
        "https://all.api.radio-browser.info"
    ];

    public IReadOnlyList<RadioStation> Stations => _stations;
    public RadioStation? CurrentStation => _currentStation;
    public RadioPlaybackState PlaybackState => _playbackState;
    public string StatusMessage => _statusMessage;

    public double Volume
    {
        get => _settingsService.CurrentSettings.RadioVolume;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_settingsService.CurrentSettings.RadioVolume - clamped) > 0.001)
            {
                _settingsService.CurrentSettings.RadioVolume = clamped;
                _settingsService.Save();
                ApplyVolumeToPlayer();
                RunOnUiThread(() => StateChanged?.Invoke());
            }
        }
    }

    public bool IsMuted
    {
        get => _settingsService.CurrentSettings.RadioMuted;
        set
        {
            if (_settingsService.CurrentSettings.RadioMuted != value)
            {
                _settingsService.CurrentSettings.RadioMuted = value;
                _settingsService.Save();
                ApplyVolumeToPlayer();
                RunOnUiThread(() => StateChanged?.Invoke());
            }
        }
    }

    public event Action? StateChanged;
    public event Action<RadioStation?>? StationChanged;

    public RadioService(ISettingsService settingsService, HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _httpClient = httpClient ?? CreateHttpClient();

        InitializeStations();
        RestoreLastStation();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DRANOTCH/1.5 (+https://github.com/dranotch)");
        return client;
    }

    private void ApplyVolumeToPlayer()
    {
        lock (_playerLock)
        {
            if (_mediaPlayer != null)
            {
                try
                {
                    _mediaPlayer.IsMuted = _settingsService.CurrentSettings.RadioMuted;
                    _mediaPlayer.Volume = _settingsService.CurrentSettings.RadioMuted
                        ? 0.0
                        : Math.Clamp(_settingsService.CurrentSettings.RadioVolume, 0.0, 1.0);
                }
                catch { }
            }
        }
    }

    private void InitializeStations()
    {
        _stations.Clear();

        var builtIn = GetBuiltInStations();
        var favIds = _settingsService.CurrentSettings.FavoriteRadioStationIds ?? new List<string>();
        var favSet = new HashSet<string>(favIds, StringComparer.OrdinalIgnoreCase);

        foreach (var st in builtIn)
        {
            st.IsFavorite = favSet.Contains(st.Id);
            _stations.Add(st);
        }

        if (_settingsService.CurrentSettings.CustomRadioStations != null)
        {
            foreach (var custom in _settingsService.CurrentSettings.CustomRadioStations)
            {
                custom.IsCustom = true;
                custom.IsFavorite = favSet.Contains(custom.Id) || custom.IsFavorite;
                _stations.Add(custom);
            }
        }
    }

    private void RestoreLastStation()
    {
        var lastId = _settingsService.CurrentSettings.LastRadioStationId;
        if (!string.IsNullOrWhiteSpace(lastId))
        {
            _currentStation = _stations.FirstOrDefault(s => string.Equals(s.Id, lastId, StringComparison.OrdinalIgnoreCase));
        }

        _currentStation ??= _stations.FirstOrDefault();
        if (_currentStation != null)
        {
            _currentStation.IsCurrent = true;
        }
    }

    private static List<RadioStation> GetBuiltInStations() =>
    [
        // ================= INDONESIA =================
        new RadioStation
        {
            Id = "id-prambors",
            Name = "Prambors FM 102.2",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Top 40 & Hits Anak Muda",
            Bitrate = 128,
            StreamUrl = "https://stream.rcs.revma.com/h77wwp48kxcwv",
            FallbackUrls =
            [
                "https://stream.zeno.fm/wvq442iwm6ztv",
                "https://svara-stream.radioddns.net/bandung_dahliafm"
            ]
        },
        new RadioStation
        {
            Id = "id-hardrock",
            Name = "Hard Rock FM 87.6",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Lifestyle, Pop & Rock",
            Bitrate = 128,
            StreamUrl = "https://stream.radiojar.com/7csmg90fuqruv",
            FallbackUrls =
            [
                "http://stream.radiojar.com/7csmg90fuqruv",
                "https://stream.denger.in/"
            ]
        },
        new RadioStation
        {
            Id = "id-iradio",
            Name = "I-Radio FM 89.6",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "100% Musik Indonesia",
            Bitrate = 128,
            StreamUrl = "https://stream.radiojar.com/4ywdgup3bnzuv",
            FallbackUrls =
            [
                "http://stream.radiojar.com/4ywdgup3bnzuv",
                "https://stream.denger.in/"
            ]
        },
        new RadioStation
        {
            Id = "id-sonora",
            Name = "Sonora FM 92.0",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "News, Musik & Informasi",
            Bitrate = 128,
            StreamUrl = "https://sonora-radio.arenastreaming.com/8130/stream",
            FallbackUrls =
            [
                "https://stream-ssl.arenastreaming.com:8066/stream"
            ]
        },
        new RadioStation
        {
            Id = "id-elshinta",
            Name = "Elshinta News 90.0 FM",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Berita & Informasi Terkini",
            Bitrate = 64,
            StreamUrl = "https://stream-ssl.arenastreaming.com:8000/jakarta",
            FallbackUrls =
            [
                "https://sonora-radio.arenastreaming.com/8130/stream"
            ]
        },
        new RadioStation
        {
            Id = "id-deltafm",
            Name = "Delta FM 99.1",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Lagu Enak & Nostalgia",
            Bitrate = 128,
            StreamUrl = "https://stream.rcs.revma.com/k02rmq48kxcwv",
            FallbackUrls =
            [
                "https://stream.zeno.fm/wvq442iwm6ztv"
            ]
        },
        new RadioStation
        {
            Id = "id-mustang",
            Name = "Mustang 88.0 FM",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Top Hits & Pop Urban",
            Bitrate = 64,
            StreamUrl = "https://ic.mari.co.id:8443/mustangfm",
            FallbackUrls =
            [
                "http://103.246.184.62:1935/noice_mustangfm/mustangfm/chunklist.m3u8"
            ]
        },
        new RadioStation
        {
            Id = "id-kisfm",
            Name = "Kis FM 95.1",
            Category = "Indonesia",
            CityOrCountry = "Jakarta",
            Genre = "Love, Life & Pop Hits",
            Bitrate = 64,
            StreamUrl = "https://ic.mari.co.id:8443/kisfm",
            FallbackUrls =
            [
                "https://stream.radiokis.id/kisfm2"
            ]
        },
        new RadioStation
        {
            Id = "id-suarasurabaya",
            Name = "Suara Surabaya 100 FM",
            Category = "Indonesia",
            CityOrCountry = "Surabaya",
            Genre = "News, Interaktif & Musik",
            Bitrate = 64,
            StreamUrl = "https://c5.siar.us/proxy/ssfm/stream",
            FallbackUrls =
            [
                "https://svara-stream.radioddns.net/bandung_dahliafm"
            ]
        },
        new RadioStation
        {
            Id = "id-rripro3",
            Name = "RRI Pro 2 Nasional",
            Category = "Indonesia",
            CityOrCountry = "Nasional",
            Genre = "Musik, Budaya & Informasi",
            Bitrate = 64,
            StreamUrl = "https://stream-node2.rri.co.id/streaming/21/9221/rritjpinangpro2.mp3",
            FallbackUrls =
            [
                "https://i.klikhost.com/9006/stream"
            ]
        },

        // ================= RELIGI / ISLAMI =================
        new RadioStation
        {
            Id = "rel-rodja",
            Name = "Radio Rodja 756 AM",
            Category = "Religi",
            CityOrCountry = "Bogor / Indonesia",
            Genre = "Dakwah Sunnah & Tilawah",
            Bitrate = 128,
            StreamUrl = "https://radioislamindonesia.com/rodja.mp3",
            FallbackUrls =
            [
                "https://stream.zeno.fm/us5bsznu3rptv"
            ]
        },
        new RadioStation
        {
            Id = "rel-quran-mishary",
            Name = "Murottal Al-Qur'an 24 Jam",
            Category = "Religi",
            CityOrCountry = "Internasional",
            Genre = "Syaikh Mishary Rashid Alafasy",
            Bitrate = 128,
            StreamUrl = "https://backup.qurango.net/radio/mishary_alafasi",
            FallbackUrls =
            [
                "https://qurango.net/radio/mishary_alafasi"
            ]
        },
        new RadioStation
        {
            Id = "rel-quran-sudais",
            Name = "Al-Qur'an Syaikh As-Sudais",
            Category = "Religi",
            CityOrCountry = "Makkah",
            Genre = "Tilawah Imam Masjidil Haram",
            Bitrate = 128,
            StreamUrl = "https://backup.qurango.net/radio/abdulrahman_alsudaes",
            FallbackUrls =
            [
                "https://qurango.net/radio/abdulrahman_alsudaes"
            ]
        },
        new RadioStation
        {
            Id = "rel-muslimjogja",
            Name = "Radio Muslim & Sholawat",
            Category = "Religi",
            CityOrCountry = "Indonesia",
            Genre = "Kajian Islam, Sholawat & Murottal",
            Bitrate = 128,
            StreamUrl = "https://stream.zeno.fm/us5bsznu3rptv",
            FallbackUrls =
            [
                "https://radioislamindonesia.com/rodja.mp3"
            ]
        },

        // ================= MUSIK & LOFI =================
        new RadioStation
        {
            Id = "lofi-chill",
            Name = "Lofi Beats 24/7 Radio",
            Category = "Musik & Lofi",
            CityOrCountry = "Global",
            Genre = "Chillhop, Study & Focus Beats",
            Bitrate = 128,
            StreamUrl = "https://play.streamafrica.net/lofiradio",
            FallbackUrls =
            [
                "https://0nlineradio.radioho.st/0r-lo-fi",
                "http://usa9.fastcast4u.com/proxy/jamz?mp=/1"
            ]
        },
        new RadioStation
        {
            Id = "lofi-groovesalad",
            Name = "Chillhop & Relax Lofi",
            Category = "Musik & Lofi",
            CityOrCountry = "Global",
            Genre = "Ambient, Downtempo & Chill",
            Bitrate = 128,
            StreamUrl = "https://0nlineradio.radioho.st/0r-lo-fi",
            FallbackUrls =
            [
                "http://usa9.fastcast4u.com/proxy/jamz?mp=/1",
                "https://play.streamafrica.net/lofiradio"
            ]
        },
        new RadioStation
        {
            Id = "lofi-nightride",
            Name = "Synthwave & Retrowave FM",
            Category = "Musik & Lofi",
            CityOrCountry = "Global",
            Genre = "Retrowave, Cyber & Outrun",
            Bitrate = 192,
            StreamUrl = "https://stream.synthwaveradio.eu/listen/synthwaveradio.eu/radio.mp3",
            FallbackUrls =
            [
                "https://a5.asurahosting.com:7920/radio.mp3"
            ]
        },
        new RadioStation
        {
            Id = "music-jazz",
            Name = "Smooth Jazz 24/7",
            Category = "Musik & Lofi",
            CityOrCountry = "Global",
            Genre = "Instrumental Jazz & Lounge",
            Bitrate = 128,
            StreamUrl = "http://jking.cdnstream1.com/b22139_128mp3",
            FallbackUrls =
            [
                "https://streaming.klcbsofficial.com/listen/newage/klcbs-newage",
                "https://0nlineradio.radioho.st/lounge-ibiza-chillout-lounge"
            ]
        },
        new RadioStation
        {
            Id = "music-classical",
            Name = "Classical Orchestral FM",
            Category = "Musik & Lofi",
            CityOrCountry = "Global",
            Genre = "Piano, Symphony & Klasik",
            Bitrate = 128,
            StreamUrl = "http://relax.stream.publicradio.org/relax.mp3",
            FallbackUrls =
            [
                "http://59.120.88.155:8000/live.mp3"
            ]
        },

        // ================= INTERNASIONAL =================
        new RadioStation
        {
            Id = "intl-bbc",
            Name = "BBC World Service",
            Category = "Internasional",
            CityOrCountry = "London, UK",
            Genre = "World News & Global Talk",
            Bitrate = 128,
            StreamUrl = "https://stream.live.vc.bbcmedia.co.uk/bbc_world_service",
            FallbackUrls =
            [
                "http://stream.live.vc.bbcmedia.co.uk/bbc_world_service"
            ]
        },
        new RadioStation
        {
            Id = "intl-jpop",
            Name = "Asia DREAM J-Pop & Anime",
            Category = "Internasional",
            CityOrCountry = "Tokyo, Japan",
            Genre = "J-Pop, Anime & Japanese Hits",
            Bitrate = 128,
            StreamUrl = "https://quincy.torontocast.com:2070/stream.mp3",
            FallbackUrls =
            [
                "http://quincy.torontocast.com:2070/stream.mp3"
            ]
        },
        new RadioStation
        {
            Id = "intl-kpop",
            Name = "K-Pop Hits Radio",
            Category = "Internasional",
            CityOrCountry = "Seoul, Korea",
            Genre = "K-Pop Chart & Korean Idol Hits",
            Bitrate = 128,
            StreamUrl = "https://0n-kpop.radionetz.de/0n-kpop.mp3",
            FallbackUrls =
            [
                "https://kpop.onlyhit.us/play",
                "https://lux.radio.tvstitch.com/k-pop-sd"
            ]
        }
    ];

    public void PlayStation(RadioStation station)
    {
        if (station == null) return;

        // Add to stations list if it came from online search and isn't in the list yet
        if (!_stations.Any(s => string.Equals(s.Id, station.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _stations.Add(station);
        }

        foreach (var s in _stations)
        {
            s.IsCurrent = string.Equals(s.Id, station.Id, StringComparison.OrdinalIgnoreCase);
            s.IsPlaying = false;
        }

        _currentStation = station;
        _currentStation.IsCurrent = true;
        _currentFallbackIndex = -1;
        _triedOnlineResolve = false;
        _triedUrlsInCurrentSession.Clear();

        _settingsService.CurrentSettings.LastRadioStationId = station.Id;
        _settingsService.Save();

        _playbackState = RadioPlaybackState.Buffering;
        _statusMessage = $"Menghubungkan ke {station.Name}...";
        StationChanged?.Invoke(_currentStation);
        StateChanged?.Invoke();

        OpenStreamUrl(station.StreamUrl);
    }

    private void OpenStreamUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            int ver = Volatile.Read(ref _playAttemptVersion);
            TryNextFallbackOrResolve(ver);
            return;
        }

        _triedUrlsInCurrentSession.Add(url);
        int attemptVersion = Interlocked.Increment(ref _playAttemptVersion);
        _hasCurrentAttemptOpened = false;
        CancelConnectTimeout();

        if (System.Windows.Application.Current?.Dispatcher == null)
        {
            // Headless unit test mode
            _playbackState = RadioPlaybackState.Playing;
            if (_currentStation != null) _currentStation.IsPlaying = true;
            _statusMessage = $"Memutar {_currentStation?.Name}";
            StateChanged?.Invoke();
            return;
        }

        try
        {
            lock (_playerLock)
            {
                DisposeActivePlayerUnlocked();

                var player = new MediaPlayer
                {
                    AudioCategory = MediaPlayerAudioCategory.Media,
                    IsMuted = IsMuted,
                    Volume = IsMuted ? 0.0 : Math.Clamp(Volume, 0.0, 1.0)
                };

                try
                {
                    // Prevent radio player from overriding Windows GSMTC session used by main music player
                    player.CommandManager.IsEnabled = false;
                }
                catch { }

                player.MediaOpened += (s, e) =>
                {
                    if (attemptVersion != Volatile.Read(ref _playAttemptVersion)) return;
                    _hasCurrentAttemptOpened = true;
                    CancelConnectTimeout();

                    RunOnUiThread(() =>
                    {
                        if (attemptVersion != Volatile.Read(ref _playAttemptVersion) || _playbackState == RadioPlaybackState.Stopped)
                            return;

                        _playbackState = RadioPlaybackState.Playing;
                        if (_currentStation != null)
                        {
                            _currentStation.IsPlaying = true;
                            _statusMessage = $"LIVE • {_currentStation.Name}";
                        }
                        StateChanged?.Invoke();
                    });
                };

                player.PlaybackSession.PlaybackStateChanged += (session, args) =>
                {
                    if (attemptVersion != Volatile.Read(ref _playAttemptVersion)) return;
                    var state = session.PlaybackState;

                    RunOnUiThread(() =>
                    {
                        if (attemptVersion != Volatile.Read(ref _playAttemptVersion) || _playbackState == RadioPlaybackState.Stopped)
                            return;

                        if (state == MediaPlaybackState.Playing)
                        {
                            _hasCurrentAttemptOpened = true;
                            CancelConnectTimeout();
                            _playbackState = RadioPlaybackState.Playing;
                            if (_currentStation != null)
                            {
                                _currentStation.IsPlaying = true;
                                _statusMessage = $"LIVE • {_currentStation.Name}";
                            }
                            StateChanged?.Invoke();
                        }
                        else if (state == MediaPlaybackState.Buffering && !_hasCurrentAttemptOpened)
                        {
                            _playbackState = RadioPlaybackState.Buffering;
                            if (_currentStation != null)
                            {
                                _statusMessage = $"Buffering {_currentStation.Name}...";
                            }
                            StateChanged?.Invoke();
                        }
                    });
                };

                player.MediaFailed += (s, e) =>
                {
                    if (attemptVersion != Volatile.Read(ref _playAttemptVersion)) return;
                    CancelConnectTimeout();
                    RunOnUiThread(() => TryNextFallbackOrResolve(attemptVersion));
                };

                var source = MediaSource.CreateFromUri(new Uri(url));
                _currentMediaSource = source;
                _mediaPlayer = player;
                player.Source = source;
                player.Play();
            }

            StartConnectTimeout(attemptVersion);
        }
        catch
        {
            RunOnUiThread(() => TryNextFallbackOrResolve(attemptVersion));
        }
    }

    private void DisposeActivePlayerUnlocked()
    {
        var oldPlayer = _mediaPlayer;
        var oldSource = _currentMediaSource;
        _mediaPlayer = null;
        _currentMediaSource = null;

        try
        {
            if (oldPlayer != null)
            {
                oldPlayer.Pause();
                oldPlayer.Source = null;
                oldPlayer.Dispose();
            }
        }
        catch { }

        try
        {
            oldSource?.Dispose();
        }
        catch { }
    }

    private void StartConnectTimeout(int attemptVersion)
    {
        var cts = new CancellationTokenSource();
        _connectTimeoutCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                if (!cts.Token.IsCancellationRequested &&
                    attemptVersion == Volatile.Read(ref _playAttemptVersion) &&
                    !_hasCurrentAttemptOpened &&
                    _playbackState == RadioPlaybackState.Buffering)
                {
                    RunOnUiThread(() => TryNextFallbackOrResolve(attemptVersion));
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void CancelConnectTimeout()
    {
        try
        {
            _connectTimeoutCts?.Cancel();
            _connectTimeoutCts?.Dispose();
            _connectTimeoutCts = null;
        }
        catch { }
    }

    private static string ExtractSearchKeyword(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName)) return string.Empty;

        // Remove frequencies (e.g. 102.2, 87.6, 756), FM/AM, 24/7, 24 Jam
        var cleaned = Regex.Replace(stationName, @"\b\d+(\.\d+)?\b", " ");
        cleaned = Regex.Replace(cleaned, @"\b(FM|AM|24/7|24\s*Jam|Nasional|Radio)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? stationName.Trim() : cleaned;
    }

    private void TryNextFallbackOrResolve(int expectedAttemptVersion)
    {
        if (expectedAttemptVersion != Volatile.Read(ref _playAttemptVersion))
        {
            return;
        }

        if (_currentStation == null || _playbackState == RadioPlaybackState.Stopped)
        {
            return;
        }

        var fallbacks = _currentStation.FallbackUrls;
        if (fallbacks != null && fallbacks.Count > 0)
        {
            while (true)
            {
                _currentFallbackIndex++;
                if (_currentFallbackIndex < 0 || _currentFallbackIndex >= fallbacks.Count)
                {
                    break;
                }

                var candidate = fallbacks[_currentFallbackIndex];
                if (!string.IsNullOrWhiteSpace(candidate) && !_triedUrlsInCurrentSession.Contains(candidate))
                {
                    _statusMessage = $"Mencoba jalur cadangan {_currentStation.Name}...";
                    StateChanged?.Invoke();
                    OpenStreamUrl(candidate);
                    return;
                }
            }
        }

        if (!_triedOnlineResolve)
        {
            _triedOnlineResolve = true;
            var stationToResolve = _currentStation;
            int resolveVersion = Volatile.Read(ref _playAttemptVersion);
            _statusMessage = $"Mencari jalur aktif {stationToResolve.Name}...";
            StateChanged?.Invoke();

            _ = Task.Run(async () =>
            {
                try
                {
                    var searchKeyword = ExtractSearchKeyword(stationToResolve.Name);
                    var results = await SearchOnlineStationsAsync(searchKeyword);
                    var freshUrls = results
                        .Select(r => r.StreamUrl)
                        .Where(u => !string.IsNullOrWhiteSpace(u) && !_triedUrlsInCurrentSession.Contains(u))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToList();

                    if (freshUrls.Count > 0 &&
                        _currentStation == stationToResolve &&
                        resolveVersion == Volatile.Read(ref _playAttemptVersion))
                    {
                        RunOnUiThread(() =>
                        {
                            if (_currentStation != stationToResolve || resolveVersion != Volatile.Read(ref _playAttemptVersion))
                                return;

                            stationToResolve.FallbackUrls ??= new List<string>();
                            foreach (var u in freshUrls)
                            {
                                if (!stationToResolve.FallbackUrls.Contains(u, StringComparer.OrdinalIgnoreCase))
                                {
                                    stationToResolve.FallbackUrls.Add(u);
                                }
                            }

                            var firstNewUrl = freshUrls[0];
                            OpenStreamUrl(firstNewUrl);
                        });
                        return;
                    }
                }
                catch { }

                RunOnUiThread(() =>
                {
                    if (_currentStation == stationToResolve && resolveVersion == Volatile.Read(ref _playAttemptVersion))
                    {
                        stationToResolve.IsPlaying = false;
                        _playbackState = RadioPlaybackState.Error;
                        _statusMessage = $"Siaran {stationToResolve.Name} sedang sibuk/offline";
                        StateChanged?.Invoke();
                    }
                });
            });
            return;
        }

        _currentStation.IsPlaying = false;
        _playbackState = RadioPlaybackState.Error;
        _statusMessage = $"Siaran {_currentStation.Name} sedang sibuk/offline";
        StateChanged?.Invoke();
    }

    public void TogglePlayPause()
    {
        if (_currentStation == null)
        {
            var first = _stations.FirstOrDefault();
            if (first != null)
            {
                PlayStation(first);
            }
            return;
        }

        if (_playbackState == RadioPlaybackState.Playing || _playbackState == RadioPlaybackState.Buffering)
        {
            Stop();
        }
        else
        {
            PlayStation(_currentStation);
        }
    }

    public void Stop()
    {
        Interlocked.Increment(ref _playAttemptVersion);
        _hasCurrentAttemptOpened = false;
        CancelConnectTimeout();

        foreach (var s in _stations)
        {
            s.IsPlaying = false;
        }

        if (_currentStation != null)
        {
            _currentStation.IsPlaying = false;
        }

        _playbackState = RadioPlaybackState.Stopped;
        _statusMessage = _currentStation != null ? $"Dihentikan • {_currentStation.Name}" : "Siap memutar radio";

        lock (_playerLock)
        {
            DisposeActivePlayerUnlocked();
        }

        RunOnUiThread(() => StateChanged?.Invoke());
    }

    public void NextStation(IEnumerable<RadioStation>? currentPool = null)
    {
        var list = (currentPool?.ToList() is { Count: > 0 } pool) ? pool : _stations;
        if (list.Count == 0) return;

        int idx = _currentStation != null
            ? list.FindIndex(s => string.Equals(s.Id, _currentStation.Id, StringComparison.OrdinalIgnoreCase))
            : -1;
        int nextIdx = idx < 0 ? 0 : (idx + 1) % list.Count;
        nextIdx = Math.Clamp(nextIdx, 0, list.Count - 1);
        PlayStation(list[nextIdx]);
    }

    public void PreviousStation(IEnumerable<RadioStation>? currentPool = null)
    {
        var list = (currentPool?.ToList() is { Count: > 0 } pool) ? pool : _stations;
        if (list.Count == 0) return;

        int idx = _currentStation != null
            ? list.FindIndex(s => string.Equals(s.Id, _currentStation.Id, StringComparison.OrdinalIgnoreCase))
            : 0;
        if (idx < 0) idx = 0;
        int prevIdx = (idx - 1 + list.Count) % list.Count;
        prevIdx = Math.Clamp(prevIdx, 0, list.Count - 1);
        PlayStation(list[prevIdx]);
    }

    public void ToggleFavorite(RadioStation station)
    {
        if (station == null) return;

        station.IsFavorite = !station.IsFavorite;

        var favList = _settingsService.CurrentSettings.FavoriteRadioStationIds ?? new List<string>();
        if (station.IsFavorite)
        {
            if (!favList.Contains(station.Id, StringComparer.OrdinalIgnoreCase))
            {
                favList.Add(station.Id);
            }
        }
        else
        {
            favList.RemoveAll(id => string.Equals(id, station.Id, StringComparison.OrdinalIgnoreCase));
        }

        _settingsService.CurrentSettings.FavoriteRadioStationIds = favList;
        _settingsService.Save();
        StateChanged?.Invoke();
    }

    public RadioStation AddCustomStation(string name, string streamUrl, string category = "Custom", string cityOrCountry = "Custom Stream")
    {
        var station = new RadioStation
        {
            Id = "custom-" + Guid.NewGuid().ToString("N")[..8],
            Name = name.Trim(),
            StreamUrl = streamUrl.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Custom" : category.Trim(),
            CityOrCountry = string.IsNullOrWhiteSpace(cityOrCountry) ? "Custom Stream" : cityOrCountry.Trim(),
            Genre = "URL Stream",
            Bitrate = 128,
            IsCustom = true
        };

        _stations.Add(station);
        _settingsService.CurrentSettings.CustomRadioStations ??= new List<RadioStation>();
        _settingsService.CurrentSettings.CustomRadioStations.Add(station);
        _settingsService.Save();
        StateChanged?.Invoke();

        return station;
    }

    public void RemoveCustomStation(RadioStation station)
    {
        if (station == null || !station.IsCustom) return;

        if (_currentStation == station)
        {
            Stop();
            _currentStation = _stations.FirstOrDefault(s => s != station);
        }

        _stations.Remove(station);
        _settingsService.CurrentSettings.CustomRadioStations?.RemoveAll(s => string.Equals(s.Id, station.Id, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save();
        StateChanged?.Invoke();
    }

    public async Task<List<RadioStation>> SearchOnlineStationsAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<RadioStation>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var encoded = Uri.EscapeDataString(query.Trim());
        var favIds = _settingsService.CurrentSettings.FavoriteRadioStationIds ?? new List<string>();
        var favSet = new HashSet<string>(favIds, StringComparer.OrdinalIgnoreCase);

        foreach (var mirror in RadioBrowserMirrors)
        {
            try
            {
                var url = $"{mirror}/json/stations/search?name={encoded}&limit=25&hidebroken=true&order=clickcount&reverse=true";
                using var resp = await _httpClient.GetAsync(url, cancellationToken);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    var name = elem.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null;
                    var resolvedUrl = elem.TryGetProperty("url_resolved", out var ur) ? ur.GetString()?.Trim() : null;
                    var rawUrl = elem.TryGetProperty("url", out var u) ? u.GetString()?.Trim() : null;
                    var streamUrl = !string.IsNullOrWhiteSpace(resolvedUrl) ? resolvedUrl : rawUrl;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(streamUrl))
                        continue;

                    var uuid = elem.TryGetProperty("stationuuid", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString("N");
                    var country = elem.TryGetProperty("country", out var cProp) ? cProp.GetString() ?? "" : "";
                    var state = elem.TryGetProperty("state", out var sProp) ? sProp.GetString() ?? "" : "";
                    var tags = elem.TryGetProperty("tags", out var tProp) ? tProp.GetString() ?? "" : "";
                    var bitrate = elem.TryGetProperty("bitrate", out var bProp) && bProp.TryGetInt32(out var bVal) ? bVal : 128;

                    var firstTags = string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2));
                    var location = !string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(country)
                        ? $"{state}, {country}"
                        : (!string.IsNullOrWhiteSpace(country) ? country : "Global");

                    var stationId = $"rb-{uuid}";
                    results.Add(new RadioStation
                    {
                        Id = stationId,
                        Name = name.Replace("\n", " ").Replace("\r", "").Trim(),
                        Category = country.Contains("Indonesia", StringComparison.OrdinalIgnoreCase) ? "Indonesia" : "Internasional",
                        StreamUrl = streamUrl,
                        CityOrCountry = location,
                        Genre = string.IsNullOrWhiteSpace(firstTags) ? "Online Radio" : firstTags,
                        Bitrate = bitrate > 0 ? bitrate : 128,
                        IsFavorite = favSet.Contains(stationId),
                        IsCurrent = _currentStation != null && string.Equals(_currentStation.Id, stationId, StringComparison.OrdinalIgnoreCase),
                        IsPlaying = _currentStation != null && string.Equals(_currentStation.Id, stationId, StringComparison.OrdinalIgnoreCase) && _playbackState == RadioPlaybackState.Playing
                    });
                }

                if (results.Count > 0)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Try next mirror
            }
        }

        return results;
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            Stop();
            _httpClient.Dispose();
        }
        catch { }
    }
}
