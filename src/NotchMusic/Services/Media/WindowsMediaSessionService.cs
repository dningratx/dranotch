using System.IO;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using NotchMusic.Models;

namespace NotchMusic.Services.Media;

public class MediaSessionInfo
{
    public string Id { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public interface IMediaSessionService : IDisposable
{
    event EventHandler<TrackInfo?>? TrackChanged;
    event EventHandler<TimeSpan>? TimelineUpdated;
    event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

    TrackInfo? CurrentTrack { get; }
    IReadOnlyList<MediaSessionInfo> AvailableSessions { get; }
    string? SelectedSessionId { get; }

    Task InitializeAsync();
    void SelectSession(string sessionId);
    Task TogglePlayPauseAsync();
    Task PreviousAsync();
    Task NextAsync();
    Task SeekAsync(TimeSpan position);
}

public class WindowsMediaSessionService : IMediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly object _lock = new();

    private TrackInfo? _currentTrack;
    private string? _selectedSessionId;
    private readonly List<MediaSessionInfo> _sessions = new();
    private bool _isDisposed;

    public event EventHandler<TrackInfo?>? TrackChanged;
    public event EventHandler<TimeSpan>? TimelineUpdated;
    public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

    public TrackInfo? CurrentTrack => _currentTrack;
    public IReadOnlyList<MediaSessionInfo> AvailableSessions
    {
        get
        {
            lock (_lock)
            {
                return _sessions.ToList();
            }
        }
    }
    public string? SelectedSessionId => _selectedSessionId;

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager != null)
            {
                _manager.SessionsChanged += Manager_SessionsChanged;
                _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                UpdateSessionsList();
                BindToSession(_manager.GetCurrentSession());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to init GSMTC: {ex.Message}");
        }
    }

    private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        UpdateSessionsList();
        lock (_lock)
        {
            // If current session is null or disconnected, rebind
            if (_currentSession == null || !IsSessionStillActive(_currentSession))
            {
                BindToBestSession();
            }
        }
    }

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        lock (_lock)
        {
            // If user explicitly picked a session and it's active, keep it
            if (!string.IsNullOrEmpty(_selectedSessionId))
            {
                var target = sender.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == _selectedSessionId);
                if (target != null)
                {
                    BindToSession(target);
                    return;
                }
            }

            BindToSession(sender.GetCurrentSession());
        }
    }

    private bool IsSessionStillActive(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return _manager != null && _manager.GetSessions().Any(s => s.SourceAppUserModelId == session.SourceAppUserModelId);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateSessionsList()
    {
        if (_manager == null) return;

        try
        {
            var sessions = _manager.GetSessions();
            var list = new List<MediaSessionInfo>();
            foreach (var s in sessions)
            {
                var appId = s.SourceAppUserModelId ?? "Unknown";
                var displayName = FormatAppDisplayName(appId);
                list.Add(new MediaSessionInfo
                {
                    Id = appId,
                    AppId = appId,
                    DisplayName = displayName
                });
            }

            lock (_lock)
            {
                _sessions.Clear();
                _sessions.AddRange(list);
            }

            SessionsChanged?.Invoke(this, AvailableSessions);
        }
        catch
        {
        }
    }

    private static string FormatAppDisplayName(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "Unknown Player";
        var lower = appId.ToLowerInvariant();
        if (lower.Contains("dranotch") || lower.Contains("notchmusic") || lower.Contains("msedgewebview2")) return "YouTube Music (DRANOTCH Player)";
        if (lower.Contains("chrome")) return "Google Chrome (YouTube Music / Web)";
        if (lower.Contains("msedge") || lower.Contains("edge")) return "Microsoft Edge (YouTube Music / Web)";
        if (lower.Contains("brave")) return "Brave Browser";
        if (lower.Contains("firefox")) return "Mozilla Firefox";
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("itunes")) return "Apple Music / iTunes";
        return appId;
    }

    private void BindToBestSession()
    {
        if (_manager == null) return;

        try
        {
            var sessions = _manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? best = null;

            // Priority 1: User's explicitly chosen session
            if (!string.IsNullOrEmpty(_selectedSessionId))
            {
                best = sessions.FirstOrDefault(s => s.SourceAppUserModelId == _selectedSessionId);
            }

            // Priority 2: Built-in YouTube Music WebView2 Player or Browsers
            if (best == null)
            {
                best = sessions.FirstOrDefault(s =>
                {
                    var id = s.SourceAppUserModelId?.ToLowerInvariant() ?? "";
                    return id.Contains("dranotch") || id.Contains("notchmusic") || id.Contains("msedgewebview2");
                });
            }

            if (best == null)
            {
                best = sessions.FirstOrDefault(s =>
                {
                    var id = s.SourceAppUserModelId?.ToLowerInvariant() ?? "";
                    return id.Contains("chrome") || id.Contains("edge") || id.Contains("brave");
                });
            }

            // Priority 3: Any active session
            best ??= _manager.GetCurrentSession() ?? sessions.FirstOrDefault();

            BindToSession(best);
        }
        catch
        {
        }
    }

    public void SelectSession(string sessionId)
    {
        _selectedSessionId = sessionId;
        if (_manager == null) return;

        var target = _manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == sessionId);
        BindToSession(target);
    }

    private void BindToSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
            }
            catch
            {
            }
        }

        _currentSession = session;

        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
            }
            catch
            {
            }

            _ = RefreshSessionPropertiesAsync(_currentSession);
        }
        else
        {
            _currentTrack = null;
            TrackChanged?.Invoke(this, null);
        }
    }

    private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshSessionPropertiesAsync(sender);
    }

    private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshSessionPropertiesAsync(sender);
    }

    private void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        try
        {
            var timeline = sender.GetTimelineProperties();
            if (timeline != null && _currentTrack != null)
            {
                _currentTrack.Position = timeline.Position;
                _currentTrack.Duration = timeline.EndTime;
                TimelineUpdated?.Invoke(this, timeline.Position);
            }
        }
        catch
        {
        }
    }

    private async Task RefreshSessionPropertiesAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var mediaProps = await session.TryGetMediaPropertiesAsync();
            var playbackInfo = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            if (mediaProps == null)
            {
                _currentTrack = null;
                TrackChanged?.Invoke(this, null);
                return;
            }

            var controls = playbackInfo?.Controls;
            var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            BitmapImage? thumbnail = null;
            if (mediaProps.Thumbnail != null)
            {
                try
                {
                    using var stream = await mediaProps.Thumbnail.OpenReadAsync();
                    if (stream != null && stream.Size > 0)
                    {
                        var memStream = new MemoryStream();
                        using (var netStream = stream.AsStreamForRead())
                        {
                            await netStream.CopyToAsync(memStream);
                        }
                        memStream.Position = 0;

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = memStream;
                        bmp.EndInit();
                        bmp.Freeze();
                        thumbnail = bmp;
                    }
                }
                catch
                {
                    thumbnail = null;
                }
            }

            var track = new TrackInfo
            {
                Title = mediaProps.Title ?? string.Empty,
                Artist = mediaProps.Artist ?? string.Empty,
                Album = mediaProps.AlbumTitle ?? string.Empty,
                Duration = timeline?.EndTime ?? TimeSpan.Zero,
                Position = timeline?.Position ?? TimeSpan.Zero,
                IsPlaying = isPlaying,
                PlaybackRate = playbackInfo?.PlaybackRate ?? 1.0,
                Thumbnail = thumbnail,
                SessionId = session.SourceAppUserModelId ?? string.Empty,
                SourceAppId = FormatAppDisplayName(session.SourceAppUserModelId ?? string.Empty),
                CanPlayPause = controls?.IsPlayPauseToggleEnabled ?? true,
                CanPrevious = controls?.IsPreviousEnabled ?? true,
                CanNext = controls?.IsNextEnabled ?? true,
                CanSeek = controls?.IsPlaybackPositionEnabled ?? false
            };

            _currentTrack = track;
            TrackChanged?.Invoke(this, track);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading media properties: {ex.Message}");
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TryTogglePlayPauseAsync();
            }
            catch { }
        }
    }

    public async Task PreviousAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TrySkipPreviousAsync();
            }
            catch { }
        }
    }

    public async Task NextAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TrySkipNextAsync();
            }
            catch { }
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (_currentSession != null)
        {
            try
            {
                await _currentSession.TryChangePlaybackPositionAsync(position.Ticks);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_manager != null)
        {
            try
            {
                _manager.SessionsChanged -= Manager_SessionsChanged;
                _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            }
            catch { }
        }

        if (_currentSession != null)
        {
            try
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
            }
            catch { }
        }
    }
}
