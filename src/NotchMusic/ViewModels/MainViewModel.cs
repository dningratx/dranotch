using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Radio;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.Services.Update;

namespace NotchMusic.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IMediaSessionService _mediaService;
    private readonly ILyricsService _lyricsService;
    private readonly ISettingsService _settingsService;
    private readonly ITodoService _todoService;
    private readonly IGoogleDriveSyncService _googleDriveSyncService;
    private readonly IUpdateService _updateService;
    private readonly IPrayerService _prayerService;
    private readonly IPrayerReminderService _prayerReminderService;
    private readonly IRadioService _radioService;
    private readonly DispatcherTimer _interpolationTimer;

    private PrayerNotificationEvent? _activePrayerNotification;
    private DispatcherTimer? _notificationDismissTimer;
    private bool _isPrayerPanelOpen;

    private readonly ICollectionView _filteredTodos;
    private string _todoFilter = "All";
    private string _newTodoTitle = string.Empty;
    private DateTime? _newTodoDueDate;
    private bool _isSettingNewTodoDeadline;
    private DateTime? _customDeadlineDate = DateTime.Today;
    private int _customDeadlineHour = 17;
    private int _customDeadlineMinute = 0;
    private TodoNotificationEvent? _activeTodoNotification;
    private DispatcherTimer? _todoNotificationDismissTimer;
    private readonly DispatcherTimer _todoDeadlineTimer;

    // Radio Online Fields (Komponen ke-5)
    private readonly ICollectionView _filteredRadioStations;
    private string _radioCategoryFilter = "All";
    private string _radioSearchQuery = string.Empty;
    private bool _isSearchingOnlineRadio;
    private bool _isAddingCustomRadio;
    private string _customRadioName = string.Empty;
    private string _customRadioUrl = string.Empty;
    private CancellationTokenSource? _radioSearchCts;

    // Ticker Fields for Minimized / Compact Mode
    private int _currentTickerIndex = 0;
    private string _currentTickerText = "Belum ada tugas";
    private string _currentTickerBadge = string.Empty;
    private TodoItemViewModel? _currentTickerItem;

    private PanelMode _currentMode = PanelMode.Compact;
    private TrackInfo? _currentTrack;
    private SongLyrics? _currentLyrics;
    private int _activeLyricIndex = -1;
    private bool _isPinned;
    private bool _isBezelMinimized;
    private bool _isIdleMiniMode;
    private DispatcherTimer? _idleTimer;
    private bool _isClickThrough;
    private bool _isHovering;
    private bool _isHoveringWindow;
    private double _currentPositionSeconds;
    private double _durationSeconds;
    private string _formattedTime = "0:00 / 0:00";
    private string _previousLyric = string.Empty;
    private string _activeLyric = string.Empty;
    private string _nextLyric = string.Empty;
    private string _sessionStatus = "Menunggu pemutar...";
    private double _manualOffsetSeconds = 0;
    private string _searchQuery = string.Empty;
    private bool _isSearching;
    private bool _isVocalRemoverEnabled;
    private List<LrcLibSearchResult> _searchResults = new();

    private CancellationTokenSource? _lyricsCts;
    private bool _isLocalLyricsLoaded;
    private Stopwatch _playbackStopwatch = new();
    private TimeSpan _baseTrackPosition = TimeSpan.Zero;

    public event Action? RequestCollapse;
    public event Action? RequestExpand;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenYouTubeMusic;
    public event Action? RequestOpenFloatingBrowser;
    public event Action? RequestOpenRadio;
    public event Action? RequestOpenMusic;
    public event Action? RequestComponentLayoutChanged;
    public event Action<string, string>? RequestShowTrayNotification;

    public PanelMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                if (value != PanelMode.Compact && _isIdleMiniMode)
                {
                    _isIdleMiniMode = false;
                    OnPropertyChanged(nameof(IsIdleMiniMode));
                    OnPropertyChanged(nameof(ShowIdleMiniView));
                }
                if (value != PanelMode.Compact && _isBezelMinimized)
                {
                    _isBezelMinimized = false;
                    Settings.IsBezelMinimized = false;
                    _settingsService.Save();
                    OnPropertyChanged(nameof(IsBezelMinimized));
                    OnPropertyChanged(nameof(IsNotBezelMinimized));
                    OnPropertyChanged(nameof(BezelMinimizeButtonTooltip));
                    OnPropertyChanged(nameof(BezelMinimizeMenuText));
                }
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(ShowCompactView));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(IsLyricsMode));
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
            }
        }
    }

    public bool IsCompactMode => CurrentMode == PanelMode.Compact;
    public bool IsExpandedMode => CurrentMode == PanelMode.Expanded;
    public bool IsLyricsMode => CurrentMode == PanelMode.Lyrics;

    public bool IsHovering
    {
        get => _isHovering;
        private set
        {
            if (SetProperty(ref _isHovering, value))
            {
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(IsNotchActive));
            }
        }
    }

    public bool IsBezelMinimized
    {
        get => _isBezelMinimized;
        set
        {
            if (SetProperty(ref _isBezelMinimized, value))
            {
                Settings.IsBezelMinimized = value;
                _settingsService.Save();
                if (value && _currentMode != PanelMode.Compact)
                {
                    _currentMode = PanelMode.Compact;
                    OnPropertyChanged(nameof(CurrentMode));
                    OnPropertyChanged(nameof(IsCompactMode));
                    OnPropertyChanged(nameof(IsExpandedMode));
                    OnPropertyChanged(nameof(IsLyricsMode));
                }
                OnPropertyChanged(nameof(IsNotBezelMinimized));
                OnPropertyChanged(nameof(ShowCompactView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(BezelMinimizeButtonTooltip));
                OnPropertyChanged(nameof(BezelMinimizeMenuText));
            }
        }
    }

    public bool IsNotBezelMinimized => !_isBezelMinimized;
    public bool ShowCompactView => IsCompactMode && !_isBezelMinimized;

    public string BezelMinimizeButtonTooltip => IsBezelMinimized
        ? "Buka kembali Notch DRANOTCH"
        : "Minimize Notch ke Bezel Atas (Agar tidak mengganggu jendela lain)";

    public string BezelMinimizeMenuText => IsBezelMinimized
        ? "▼ Buka Kembali Notch dari Bezel"
        : "▲ Minimize Notch ke Bezel Atas";

    public bool ShowComponentIcons => IsCompactMode && !_isBezelMinimized && (_isHovering || IsPinned);
    public bool ShowMusicInNotch => IsCompactMode && !_isBezelMinimized && (ShowComponentIcons || IsPlaying);
    public bool ShowMusicPillInNotch => ShowMusicInNotch && !IsMusicWindowOpen;
    public bool ShowMusicIconInNotch => ShowMusicInNotch && IsMusicWindowOpen;
    public bool ShowIdlePrayerBesideLogo => IsCompactMode && !_isBezelMinimized && !ShowComponentIcons && Settings.EnablePrayerTimes && Settings.ShowPrayerInCompactNotch;

    public string MusicNotchTooltip => HasTrack
        ? $"{CurrentTrack?.Title} - {CurrentTrack?.Artist}\nKlik untuk perluas pemutar & lirik"
        : "Pemutar Musik DRANOTCH (Klik untuk perluas pemutar & lirik)";

    public bool HasTrackThumbnail => CurrentTrack?.Thumbnail != null;

    public string TodoNotchTooltip => HasPendingTodos
        ? $"To-Do List: {PendingTodosCount} tugas aktif\nKlik untuk buka daftar tugas"
        : "To-Do List DRANOTCH (Klik untuk buka daftar tugas)";

    public string PendingTodoCountText => PendingTodosCount > 99 ? "99+" : PendingTodosCount.ToString();

    public string PinMenuText => IsPinned ? "Lepas Pin Notch (Otomatis ke Logo saat Idle)" : "📌 Pin Notch (Tetap Tampilkan 5 Ikon)";

    public bool IsIdleMiniMode
    {
        get => _isIdleMiniMode && !IsPinned;
        set
        {
            var effective = value && !IsPinned;
            if (SetProperty(ref _isIdleMiniMode, effective))
            {
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(ShowFullMusicInCompact));
                OnPropertyChanged(nameof(ShowFullRadioInCompact));
                OnPropertyChanged(nameof(ShowFullTodoInCompact));
                OnPropertyChanged(nameof(ShowFullBrowserInCompact));
                OnPropertyChanged(nameof(MiniModeButtonTooltip));
                OnPropertyChanged(nameof(IsExpandedMode));
            }
        }
    }

    public bool ShowIdleMiniView => IsCompactMode && IsIdleMiniMode;
    public bool ShowFullMusicInCompact => !IsIdleMiniMode || IsPlaying;
    public bool ShowFullRadioInCompact => !IsIdleMiniMode || IsRadioActiveOrOpen;
    public bool ShowFullTodoInCompact => !IsIdleMiniMode || HasPendingTodos;
    public bool ShowFullBrowserInCompact => !IsIdleMiniMode || IsBrowserWindowOpen;
    public string MiniModeButtonTooltip => IsIdleMiniMode
        ? "Perbesar Notch (Tampilkan semua detail komponen)"
        : "Kecilkan Notch (Tampilkan yang aktif & ikon saja)";

    public string ComponentDisplayMode
    {
        get => Settings.ComponentDisplayMode ?? "Both";
        set
        {
            if (Settings.ComponentDisplayMode != value)
            {
                Settings.ComponentDisplayMode = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsTodoDetached
    {
        get => Settings.IsTodoDetached;
        set
        {
            if (Settings.IsTodoDetached != value)
            {
                Settings.IsTodoDetached = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsTodoWindowOpen
    {
        get => Settings.IsTodoWindowOpen;
        set
        {
            if (Settings.IsTodoWindowOpen != value)
            {
                Settings.IsTodoWindowOpen = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsPrayerDetached
    {
        get => Settings.IsPrayerDetached;
        set
        {
            if (Settings.IsPrayerDetached != value)
            {
                Settings.IsPrayerDetached = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsPrayerWindowOpen
    {
        get => Settings.IsPrayerWindowOpen;
        set
        {
            if (Settings.IsPrayerWindowOpen != value)
            {
                Settings.IsPrayerWindowOpen = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsPrayerClosed => IsPrayerDetached && !IsPrayerWindowOpen;
    public bool IsTodoClosed => IsTodoDetached && !IsTodoWindowOpen;
    public bool IsTodoDetachedWindowVisible => IsTodoDetached && IsTodoWindowOpen;
    public bool HasClosedComponents => IsPrayerClosed || IsTodoClosed;
    public int ClosedComponentsCount => (IsPrayerClosed ? 1 : 0) + (IsTodoClosed ? 1 : 0);
    public string ComponentsButtonText => HasClosedComponents ? $"Komponen ({ClosedComponentsCount})" : "Komponen";

    public bool IsBrowserWindowOpen
    {
        get => Settings.IsBrowserWindowOpen;
        set
        {
            if (Settings.IsBrowserWindowOpen != value)
            {
                Settings.IsBrowserWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBrowserWindowOpen));
                OnPropertyChanged(nameof(IsBrowserClosed));
                OnPropertyChanged(nameof(BrowserStatusSummary));
                OnPropertyChanged(nameof(BrowserButtonActionText));
                OnPropertyChanged(nameof(ShowFullBrowserInCompact));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsBrowserClosed => !IsBrowserWindowOpen;
    public string BrowserStatusSummary => IsBrowserWindowOpen ? "Jendela Melayang Aktif" : "Jendela Tertutup";
    public string BrowserButtonActionText => IsBrowserWindowOpen ? "Tutup" : "Buka";

    // Radio Online Properties (Di sebelah kanan Music Player)
    public IRadioService RadioService => _radioService;
    public ObservableCollection<RadioStation> RadioStations { get; } = new();
    public bool IsMusicWindowOpen
    {
        get => Settings.IsMusicWindowOpen;
        set
        {
            if (Settings.IsMusicWindowOpen != value)
            {
                Settings.IsMusicWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsMusicWindowOpen));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }
    public ICollectionView FilteredRadioStations => _filteredRadioStations;

    public bool IsMusicWindowCompact
    {
        get => Settings.IsMusicWindowCompact;
        set
        {
            if (Settings.IsMusicWindowCompact != value)
            {
                Settings.IsMusicWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsMusicWindowCompact));
                OnPropertyChanged(nameof(IsMusicWindowExpanded));
            }
        }
    }

    public bool IsMusicWindowExpanded => !IsMusicWindowCompact;

    public bool IsRadioWindowOpen
    {
        get => Settings.IsRadioWindowOpen;
        set
        {
            if (Settings.IsRadioWindowOpen != value)
            {
                Settings.IsRadioWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsRadioWindowOpen));
                OnPropertyChanged(nameof(IsRadioActiveOrOpen));
                OnPropertyChanged(nameof(RadioStatusSummary));
                OnPropertyChanged(nameof(RadioButtonActionText));
                OnPropertyChanged(nameof(ShowFullRadioInCompact));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsRadioWindowCompact
    {
        get => Settings.IsRadioWindowCompact;
        set
        {
            if (Settings.IsRadioWindowCompact != value)
            {
                Settings.IsRadioWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsRadioWindowCompact));
                OnPropertyChanged(nameof(IsRadioWindowExpanded));
            }
        }
    }

    public bool IsRadioWindowExpanded => !IsRadioWindowCompact;

    public RadioStation? CurrentRadioStation => _radioService.CurrentStation;
    public string CurrentRadioStationName => _radioService.CurrentStation?.Name ?? "Pilih Stasiun Radio";
    public string CurrentRadioStationSubtitle => _radioService.CurrentStation?.Subtitle ?? "Radio Online Indonesia & Dunia";
    public string CurrentRadioFavoriteSymbol => _radioService.CurrentStation?.FavoriteSymbol ?? "☆";

    public bool IsRadioPlaying => _radioService.PlaybackState == RadioPlaybackState.Playing;
    public bool IsRadioBuffering => _radioService.PlaybackState == RadioPlaybackState.Buffering;
    public bool IsRadioActiveOrOpen => IsRadioPlaying || IsRadioBuffering || IsRadioWindowOpen;

    public string RadioStateBadgeText => _radioService.PlaybackState switch
    {
        RadioPlaybackState.Playing => "LIVE",
        RadioPlaybackState.Buffering => "BUFFERING",
        RadioPlaybackState.Error => "OFFLINE",
        _ => "SIAP"
    };

    public string RadioStatusMessage => _radioService.StatusMessage;

    public string RadioCompactLabel => (IsRadioPlaying || IsRadioBuffering) && _radioService.CurrentStation != null
        ? _radioService.CurrentStation.Name
        : "Radio";

    public string RadioNotchTooltip => _radioService.CurrentStation != null
        ? $"Radio Online: {_radioService.CurrentStation.Name} ({RadioStateBadgeText})\nKlik untuk membuka daftar stasiun & pemutar Radio Online"
        : "Buka Radio Online (Streaming Radio Indonesia & Dunia)";

    public string RadioStatusSummary => IsRadioPlaying && _radioService.CurrentStation != null
        ? $"LIVE: {_radioService.CurrentStation.Name}"
        : (IsRadioWindowOpen ? "Jendela Radio Aktif" : "Siap Memutar");

    public string RadioButtonActionText => IsRadioWindowOpen ? "Tutup" : "Buka";

    public double RadioVolume
    {
        get => _radioService.Volume;
        set
        {
            _radioService.Volume = value;
            if (value > 0 && _radioService.IsMuted)
            {
                _radioService.IsMuted = false;
            }
            OnPropertyChanged(nameof(RadioVolume));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
        }
    }

    public bool IsRadioMuted
    {
        get => _radioService.IsMuted;
        set
        {
            _radioService.IsMuted = value;
            OnPropertyChanged(nameof(IsRadioMuted));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
        }
    }

    public string RadioVolumePercentText => IsRadioMuted ? "Mute" : $"{Math.Round(RadioVolume * 100)}%";
    public string RadioVolumeIcon => IsRadioMuted || RadioVolume <= 0.01 ? "🔇" : (RadioVolume < 0.5 ? "🔉" : "🔊");

    public string RadioCategoryFilter
    {
        get => _radioCategoryFilter;
        set
        {
            if (SetProperty(ref _radioCategoryFilter, value))
            {
                _filteredRadioStations?.Refresh();
            }
        }
    }

    public string RadioSearchQuery
    {
        get => _radioSearchQuery;
        set
        {
            if (SetProperty(ref _radioSearchQuery, value))
            {
                _filteredRadioStations?.Refresh();
            }
        }
    }

    public bool IsSearchingOnlineRadio
    {
        get => _isSearchingOnlineRadio;
        set
        {
            if (SetProperty(ref _isSearchingOnlineRadio, value))
            {
                OnPropertyChanged(nameof(SearchOnlineRadioButtonText));
            }
        }
    }

    public string SearchOnlineRadioButtonText => IsSearchingOnlineRadio ? "Mencari..." : "🌐 Cari Online";

    public bool IsAddingCustomRadio
    {
        get => _isAddingCustomRadio;
        set => SetProperty(ref _isAddingCustomRadio, value);
    }

    public string CustomRadioName
    {
        get => _customRadioName;
        set => SetProperty(ref _customRadioName, value);
    }

    public string CustomRadioUrl
    {
        get => _customRadioUrl;
        set => SetProperty(ref _customRadioUrl, value);
    }

    public string ComponentsButtonSymbol
    {
        get
        {
            if (IsPrayerClosed && IsTodoClosed)
                return "🕌 ✓";
            if (IsPrayerClosed)
                return "🕌";
            if (IsTodoClosed)
                return "✓";
            return "🍱";
        }
    }

    public string ComponentsButtonTooltip
    {
        get
        {
            if (IsPrayerClosed && IsTodoClosed)
                return "Komponen ditutup: Waktu Sholat & To-Do List (Klik untuk kelola/buka)";
            if (IsPrayerClosed)
                return "Komponen ditutup: Waktu Sholat (Klik untuk kelola/buka)";
            if (IsTodoClosed)
                return "Komponen ditutup: To-Do List (Klik untuk kelola/buka)";
            return "Kelola komponen notch";
        }
    }

    private bool _isComponentsMenuOpen;
    public bool IsComponentsMenuOpen
    {
        get => _isComponentsMenuOpen;
        set => SetProperty(ref _isComponentsMenuOpen, value);
    }

    public bool IsComponentBoth => ComponentDisplayMode == "Both";
    public bool IsComponentMusicOnly => ComponentDisplayMode == "MusicOnly";
    public bool IsComponentTodoOnly => ComponentDisplayMode == "TodoOnly";

    public bool ShowMusicSection => ComponentDisplayMode != "TodoOnly";
    public bool ShowTodoSection => ComponentDisplayMode != "MusicOnly";
    public bool ShowTodoTicker => ShowTodoSection && (!IsTodoDetached || ComponentDisplayMode == "TodoOnly") && IsTodoWindowOpen;
    public bool ShowTodoSectionInMainWindow => ShowTodoSection && (!IsTodoDetached || ComponentDisplayMode == "TodoOnly") && IsTodoWindowOpen;
    public bool ShowMusicMinimizeButton => !ShowTodoSectionInMainWindow;
    public bool ShowSplitter => ShowExpandedPrayerDivider2;

    public bool IsTodoWindowCompact
    {
        get => Settings.IsTodoWindowCompact;
        set
        {
            if (Settings.IsTodoWindowCompact != value)
            {
                Settings.IsTodoWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsTodoWindowCompact));
                OnPropertyChanged(nameof(IsTodoWindowExpanded));
            }
        }
    }

    public bool IsTodoWindowExpanded => !IsTodoWindowCompact;

    public bool IsPrayerWindowCompact
    {
        get => Settings.IsPrayerWindowCompact;
        set
        {
            if (Settings.IsPrayerWindowCompact != value)
            {
                Settings.IsPrayerWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsPrayerWindowCompact));
                OnPropertyChanged(nameof(IsPrayerWindowExpanded));
            }
        }
    }

    public bool IsPrayerWindowExpanded => !IsPrayerWindowCompact;

    public bool ShowCompactMusicView => IsCompactMode && ShowMusicSection;
    public bool ShowCompactTodoOnlyView => IsCompactMode && !ShowMusicSection && ShowTodoSection;

    public bool ShowPrayerDivider1 => ShowMusicSection && ShowCompactPrayerPill;
    public bool ShowPrayerDivider2 => ShowTodoTicker && (ShowMusicSection || ShowCompactPrayerPill);

    public bool ShowPrayerSectionInMainWindow => EnablePrayerTimes && !IsPrayerDetached && IsPrayerWindowOpen;
    public bool ShowExpandedPrayerDivider1 => ShowMusicSection && ShowPrayerSectionInMainWindow;
    public bool ShowExpandedPrayerDivider2 => ShowTodoSectionInMainWindow && (ShowMusicSection || ShowPrayerSectionInMainWindow);

    public System.Windows.GridLength MusicColumnWidth => ShowMusicSection ? new System.Windows.GridLength(410) : new System.Windows.GridLength(0);
    public System.Windows.GridLength PrayerColumnWidth => ShowPrayerSectionInMainWindow ? new System.Windows.GridLength(285) : new System.Windows.GridLength(0);
    public System.Windows.GridLength TodoColumnWidth => ShowTodoSectionInMainWindow ? new System.Windows.GridLength(410) : new System.Windows.GridLength(0);

    public bool IsPrayerPanelOpen
    {
        get => _isPrayerPanelOpen;
        set
        {
            if (SetProperty(ref _isPrayerPanelOpen, value))
            {
                OnPropertyChanged(nameof(ShowNormalExpandedContent));
                OnPropertyChanged(nameof(ShowPrayerExpandedContent));
                OnPropertyChanged(nameof(ExpandedPanelWidth));
            }
        }
    }

    public bool ShowNormalExpandedContent => !IsPrayerPanelOpen;
    public bool ShowPrayerExpandedContent => IsPrayerPanelOpen;

    public double ExpandedPanelWidth
    {
        get
        {
            if (IsPrayerPanelOpen)
                return 460;

            if (ShowMusicSection && ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 1150;
            if (ShowMusicSection && ShowPrayerSectionInMainWindow)
                return 716;
            if (ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 720;
            if (ShowMusicSection && ShowTodoSectionInMainWindow)
                return 845;

            if (ShowMusicSection)
                return 410;
            if (ShowTodoSectionInMainWindow)
                return 410;
            if (ShowPrayerSectionInMainWindow)
                return 285;

            return 410;
        }
    }

    public TrackInfo? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (SetProperty(ref _currentTrack, value))
            {
                OnPropertyChanged(nameof(StandbyLyric));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(IsAudioActive));
                OnPropertyChanged(nameof(ShowFullMusicInCompact));
                OnPropertyChanged(nameof(IsNotchActive));
                OnPropertyChanged(nameof(HasTrack));
                OnPropertyChanged(nameof(HasTrackThumbnail));
                OnPropertyChanged(nameof(MusicNotchTooltip));
            }
        }
    }

    public bool HasTrack => CurrentTrack != null && !string.IsNullOrWhiteSpace(CurrentTrack.Title);

    public bool IsPlaying => CurrentTrack?.IsPlaying ?? false;
    public bool IsAudioActive => IsPlaying || IsRadioPlaying;
    public bool IsNotchActive => IsAudioActive || IsRadioBuffering || IsRadioWindowOpen || IsBrowserWindowOpen || CurrentMode != PanelMode.Compact || IsComponentsMenuOpen || HasActivePrayerNotification || _isHovering;

    public SongLyrics? CurrentLyrics
    {
        get => _currentLyrics;
        private set
        {
            if (SetProperty(ref _currentLyrics, value))
            {
                OnPropertyChanged(nameof(IsLyricsSynced));
            }
        }
    }

    public bool IsLyricsSynced => CurrentLyrics?.IsSynced == true;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                Settings.IsPinned = value;
                _settingsService.Save();
                if (value)
                {
                    _idleTimer?.Stop();
                    if (_isIdleMiniMode)
                    {
                        _isIdleMiniMode = false;
                    }
                }
                else if (!_isHovering)
                {
                    ResetIdleTimer();
                }
                OnPropertyChanged(nameof(IsIdleMiniMode));
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(ShowFullMusicInCompact));
                OnPropertyChanged(nameof(ShowFullRadioInCompact));
                OnPropertyChanged(nameof(ShowFullTodoInCompact));
                OnPropertyChanged(nameof(ShowFullBrowserInCompact));
                OnPropertyChanged(nameof(MiniModeButtonTooltip));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(PinButtonTooltip));
                OnPropertyChanged(nameof(PinMenuText));
            }
        }
    }

    public string PinButtonTooltip => IsPinned
        ? "Notch sedang di-Pin (Tidak akan mengecil saat idle) — Klik untuk lepas Pin"
        : "Pin Notch agar tidak berubah menjadi kecil saat idle";

    public bool IsClickThrough
    {
        get => _isClickThrough;
        set => SetProperty(ref _isClickThrough, value);
    }

    public double CurrentPositionSeconds
    {
        get => _currentPositionSeconds;
        set => SetProperty(ref _currentPositionSeconds, value);
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        set => SetProperty(ref _durationSeconds, value);
    }

    public string FormattedTime
    {
        get => _formattedTime;
        set => SetProperty(ref _formattedTime, value);
    }

    public string PreviousLyric
    {
        get => _previousLyric;
        set => SetProperty(ref _previousLyric, value);
    }

    public string ActiveLyric
    {
        get => _activeLyric;
        set
        {
            if (SetProperty(ref _activeLyric, value))
            {
                OnPropertyChanged(nameof(StandbyLyric));
            }
        }
    }

    public string StandbyLyric
    {
        get
        {
            if (CurrentTrack == null)
            {
                return "Siap memutar musik...";
            }

            if (!string.IsNullOrWhiteSpace(_activeLyric))
            {
                var firstLine = _activeLyric.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    return firstLine.Trim();
                }
            }

            return !string.IsNullOrWhiteSpace(CurrentTrack.Artist) ? CurrentTrack.Artist : "♪ Lirik belum tersedia";
        }
    }

    public string NextLyric
    {
        get => _nextLyric;
        set => SetProperty(ref _nextLyric, value);
    }

    public string SessionStatus
    {
        get => _sessionStatus;
        set => SetProperty(ref _sessionStatus, value);
    }

    public double ManualOffsetSeconds
    {
        get => _manualOffsetSeconds;
        set => SetProperty(ref _manualOffsetSeconds, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public List<LrcLibSearchResult> SearchResults
    {
        get => _searchResults;
        set => SetProperty(ref _searchResults, value);
    }

    public IReadOnlyList<MediaSessionInfo> AvailableSessions => _mediaService.AvailableSessions;

    public bool IsVocalRemoverEnabled
    {
        get => _isVocalRemoverEnabled;
        set
        {
            if (SetProperty(ref _isVocalRemoverEnabled, value))
            {
                OnPropertyChanged(nameof(VocalRemoverButtonText));
                OnPropertyChanged(nameof(VocalRemoverTooltip));
                RequestVocalRemoverChanged?.Invoke(value);
            }
        }
    }

    public string VocalRemoverButtonText => IsVocalRemoverEnabled ? "🎤 Karaoke ON" : "🎤 Karaoke";

    public string VocalRemoverTooltip => IsVocalRemoverEnabled
        ? "Penghilang Vokal (Mode Karaoke) sedang AKTIF — Klik untuk kembali ke vokal normal"
        : "Aktifkan Penghilang Vokal (Mode Karaoke) untuk meredam suara vokal penyanyi";
    public AppSettings Settings => _settingsService.CurrentSettings;

    // To-Do List Properties
    public ObservableCollection<TodoItemViewModel> Todos { get; } = new();
    public ICollectionView FilteredTodos => _filteredTodos;

    public string TodoFilter
    {
        get => _todoFilter;
        set
        {
            if (SetProperty(ref _todoFilter, value))
            {
                _filteredTodos.Refresh();
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsCompletedFilterSelected));
            }
        }
    }

    public bool IsAllFilterSelected => _todoFilter == "All";
    public bool IsActiveFilterSelected => _todoFilter == "Active";
    public bool IsCompletedFilterSelected => _todoFilter == "Completed";

    public ObservableCollection<string> AvailableCategories { get; } = new();
    public ObservableCollection<string> ExistingCategories { get; } = new();

    private string _newTodoCategory = string.Empty;
    public string NewTodoCategory
    {
        get => _newTodoCategory;
        set => SetProperty(ref _newTodoCategory, value);
    }

    private string _selectedCategoryFilter = "All";
    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _filteredTodos?.Refresh();
                OnPropertyChanged(nameof(IsAllCategorySelected));
            }
        }
    }

    public bool IsAllCategorySelected => string.Equals(_selectedCategoryFilter, "All", StringComparison.OrdinalIgnoreCase);
    public bool HasCategories => AvailableCategories.Count > 1;

    public string NewTodoTitle
    {
        get => _newTodoTitle;
        set => SetProperty(ref _newTodoTitle, value);
    }

    public DateTime? NewTodoDueDate
    {
        get => _newTodoDueDate;
        set
        {
            if (SetProperty(ref _newTodoDueDate, value))
            {
                OnPropertyChanged(nameof(HasNewTodoDueDate));
                OnPropertyChanged(nameof(NewTodoDueDateText));
            }
        }
    }

    public bool HasNewTodoDueDate => NewTodoDueDate.HasValue;

    public string NewTodoDueDateText
    {
        get
        {
            if (!NewTodoDueDate.HasValue) return string.Empty;
            var dt = NewTodoDueDate.Value;
            var today = DateTime.Today;
            if (dt.Date == today)
                return $"Hari ini {dt:HH:mm}";
            if (dt.Date == today.AddDays(1))
                return $"Besok {dt:HH:mm}";
            if (dt.Date == today.AddDays(-1))
                return $"Kemarin {dt:HH:mm}";
            if (dt.Year == today.Year)
                return dt.ToString("d MMM HH:mm");
            return dt.ToString("d MMM yyyy HH:mm");
        }
    }

    public bool IsSettingNewTodoDeadline
    {
        get => _isSettingNewTodoDeadline;
        set => SetProperty(ref _isSettingNewTodoDeadline, value);
    }

    public DateTime? CustomDeadlineDate
    {
        get => _customDeadlineDate;
        set => SetProperty(ref _customDeadlineDate, value);
    }

    public int CustomDeadlineHour
    {
        get => _customDeadlineHour;
        set => SetProperty(ref _customDeadlineHour, value);
    }

    public int CustomDeadlineMinute
    {
        get => _customDeadlineMinute;
        set => SetProperty(ref _customDeadlineMinute, value);
    }

    public bool HasActiveTodoNotification => _activeTodoNotification != null;

    public TodoNotificationEvent? ActiveTodoNotification
    {
        get => _activeTodoNotification;
        private set
        {
            if (SetProperty(ref _activeTodoNotification, value))
            {
                OnPropertyChanged(nameof(HasActiveTodoNotification));
                if (value != null)
                {
                    _todoNotificationDismissTimer?.Stop();
                    _todoNotificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
                    _todoNotificationDismissTimer.Tick += (s, e) =>
                    {
                        _todoNotificationDismissTimer.Stop();
                        ActiveTodoNotification = null;
                    };
                    _todoNotificationDismissTimer.Start();
                }
            }
        }
    }

    public int PendingTodosCount => Todos.Count(t => !t.IsCompleted);
    public int CompletedTodosCount => Todos.Count(t => t.IsCompleted);
    public int TotalTodosCount => Todos.Count;
    public bool HasCompletedTodos => CompletedTodosCount > 0;
    public bool HasPendingTodos => PendingTodosCount > 0;
    public string TodoSummaryText => TotalTodosCount == 0 ? "Belum ada tugas" : $"{CompletedTodosCount}/{TotalTodosCount} selesai";

    // Ticker Properties for Minimized / Standby Notch Mode
    public string CurrentTickerText => _currentTickerText;
    public string CurrentTickerBadge => _currentTickerBadge;
    public TodoItemViewModel? CurrentTickerItem => _currentTickerItem;

    // Google Drive Sync Properties
    public IGoogleDriveSyncService GoogleDriveSyncService => _googleDriveSyncService;
    public IUpdateService UpdateService => _updateService;
    private bool _isCheckingOrInstallingUpdate;
    private string _updateActionStatusText = string.Empty;
    public string UpdateActionStatusText
    {
        get => _updateActionStatusText;
        private set
        {
            if (SetProperty(ref _updateActionStatusText, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    public string CurrentVersionText => $"v{_updateService.CurrentVersion}";
    public string VersionButtonText => !string.IsNullOrWhiteSpace(_updateActionStatusText)
        ? _updateActionStatusText
        : (HasAvailableUpdate && !string.IsNullOrWhiteSpace(AvailableUpdateVersion)
            ? $"Click to Update {(AvailableUpdateVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? AvailableUpdateVersion : "v" + AvailableUpdateVersion)}"
            : CurrentVersionText);
    public string VersionButtonTooltip => HasAvailableUpdate && !string.IsNullOrWhiteSpace(AvailableUpdateVersion)
        ? $"Pembaruan v{AvailableUpdateVersion} tersedia! (Saat ini: {CurrentVersionText}) — Klik untuk langsung download & install"
        : $"Versi Aplikasi DRANOTCH {CurrentVersionText} — Klik untuk langsung cek, download, dan install pembaruan";

    private bool _hasAvailableUpdate;
    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        set
        {
            if (SetProperty(ref _hasAvailableUpdate, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    private string _availableUpdateVersion = string.Empty;
    public string AvailableUpdateVersion
    {
        get => _availableUpdateVersion;
        set
        {
            if (SetProperty(ref _availableUpdateVersion, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    private UpdateInfo? _availableUpdateInfo;
    public UpdateInfo? AvailableUpdateInfo
    {
        get => _availableUpdateInfo;
        set => SetProperty(ref _availableUpdateInfo, value);
    }

    public ICommand OpenUpdateCommand { get; }
    public bool IsGoogleDriveConnected => _googleDriveSyncService.IsConnected;
    public bool IsGoogleDriveSyncing => _googleDriveSyncService.IsSyncing;
    public string GoogleDriveUserEmail => _googleDriveSyncService.UserEmail;
    public string GoogleDriveSyncStatus => _googleDriveSyncService.SyncStatus;

    public string GoogleDriveStatusButtonText
    {
        get
        {
            if (IsGoogleDriveSyncing) return "Menyinkronkan...";
            if (IsGoogleDriveConnected) return "Drive";
            return "Drive: Belum Konek";
        }
    }

    public string GoogleDriveSyncTooltip => _googleDriveSyncService.IsConnected
        ? $"Google Drive ({_googleDriveSyncService.UserEmail}): {_googleDriveSyncService.SyncStatus}\nKlik untuk sinkronkan sekarang"
        : "Google Drive belum terhubung.\nKlik untuk menghubungkan dan login akun Google";

    // Prayer Times & Reminders Properties
    public IPrayerService PrayerService => _prayerService;
    public IPrayerReminderService PrayerReminderService => _prayerReminderService;
    public PrayerSchedule CurrentPrayerSchedule => _prayerReminderService.CurrentSchedule;

    public bool EnablePrayerTimes => Settings.EnablePrayerTimes;
    public bool ShowCompactPrayerPill => Settings.EnablePrayerTimes && Settings.ShowPrayerInCompactNotch && !IsPrayerDetached && IsPrayerWindowOpen && CurrentPrayerSchedule?.NextPrayer != null;

    public string NextPrayerName => CurrentPrayerSchedule?.NextPrayer?.Name ?? string.Empty;
    public string NextPrayerTimeFormatted => CurrentPrayerSchedule?.NextPrayer?.FormattedTime ?? string.Empty;
    public string NextPrayerCountdownBadge => CurrentPrayerSchedule?.NextPrayer?.CountdownText ?? string.Empty;
    public string PrayerScheduleTooltip => CurrentPrayerSchedule != null
        ? $"Jadwal Sholat ({CurrentPrayerSchedule.CityName}): {CurrentPrayerSchedule.CountdownSummary}\nKlik untuk melihat jadwal lengkap hari ini"
        : "Waktu Sholat";

    public IReadOnlyList<AdhanSoundOption> AvailableAdhanSounds => PrayerAudioHelper.AvailableSounds;

    public string SelectedAdhanSound
    {
        get => Settings.SelectedAdhanSound;
        set
        {
            if (Settings.SelectedAdhanSound != value)
            {
                Settings.SelectedAdhanSound = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedAdhanSoundName));
            }
        }
    }

    public string SelectedAdhanSoundName
    {
        get
        {
            string name;
            if (string.Equals(SelectedAdhanSound, "custom", StringComparison.OrdinalIgnoreCase))
            {
                string customName = !string.IsNullOrWhiteSpace(Settings.CustomAdhanSoundPath)
                    ? System.IO.Path.GetFileName(Settings.CustomAdhanSoundPath)
                    : "File Audio Kustom";
                name = $"Kustom: {customName}";
            }
            else
            {
                var opt = AvailableAdhanSounds.FirstOrDefault(s => string.Equals(s.Id, SelectedAdhanSound, StringComparison.OrdinalIgnoreCase));
                name = opt?.Name ?? "Adzan Mekkah (Masjidil Haram)";
            }

            if (Settings.AdhanCustomDurationEnabled)
            {
                name += $" (⏱ {Settings.AdhanStartMinutes:D2}:{Settings.AdhanStartSeconds:D2} - {Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2})";
            }
            return name;
        }
    }

    private bool _isAdhanPlaying;
    public bool IsAdhanPlaying
    {
        get => _isAdhanPlaying;
        set
        {
            if (_isAdhanPlaying != value)
            {
                _isAdhanPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanPlayButtonIcon));
                OnPropertyChanged(nameof(AdhanPlayButtonTooltip));
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
            }
        }
    }

    private bool _isAdhanPaused;
    public bool IsAdhanPaused
    {
        get => _isAdhanPaused;
        set
        {
            if (SetProperty(ref _isAdhanPaused, value))
            {
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
            }
        }
    }

    public string AdhanPlayButtonIcon => IsAdhanPlaying ? "⏹" : "▶";
    public string AdhanPlayButtonTooltip => IsAdhanPlaying ? "Berhenti putar suara" : "Tes putar suara adzan";
    public string AdhanPlayPauseButtonIcon => (!IsAdhanPlaying || IsAdhanPaused) ? "▶" : "⏸";

    private double _adhanCurrentSec;
    private double _adhanTotalSec;

    public double AdhanCurrentPositionSeconds
    {
        get => _adhanCurrentSec;
        set
        {
            if (Math.Abs(_adhanCurrentSec - value) > 0.5)
            {
                _adhanCurrentSec = value;
                PrayerAudioHelper.SeekTo(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanProgressText));
                OnPropertyChanged(nameof(AdhanProgressPercent));
            }
        }
    }

    public double AdhanTotalDurationSeconds => _adhanTotalSec > 0 ? _adhanTotalSec : 180;
    public string AdhanCurrentPositionText => TimeSpan.FromSeconds(Math.Max(0, _adhanCurrentSec)).ToString(@"mm\:ss");
    public string AdhanTotalDurationText => TimeSpan.FromSeconds(Math.Max(0, _adhanTotalSec)).ToString(@"mm\:ss");

    public string AdhanProgressText
    {
        get
        {
            string cur = TimeSpan.FromSeconds(Math.Max(0, _adhanCurrentSec)).ToString(@"mm\:ss");
            if (Settings.AdhanCustomDurationEnabled && Settings.GetAdhanEndTotalSeconds() > 0)
            {
                string end = $"{Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2}";
                return $"{cur} / {end}";
            }
            string tot = TimeSpan.FromSeconds(Math.Max(0, _adhanTotalSec)).ToString(@"mm\:ss");
            return $"{cur} / {tot}";
        }
    }

    public double AdhanProgressPercent
    {
        get
        {
            double target = (Settings.AdhanCustomDurationEnabled && Settings.GetAdhanEndTotalSeconds() > 0)
                ? Settings.GetAdhanEndTotalSeconds()
                : _adhanTotalSec;
            return target > 0 ? Math.Clamp((_adhanCurrentSec / target) * 100.0, 0, 100) : 0;
        }
        set { /* Read-only calculated property; setter provided to prevent WPF TwoWay binding crash */ }
    }
    public bool HasActivePrayerNotification => EnablePrayerTimes && _activePrayerNotification != null;
    public PrayerNotificationEvent? ActivePrayerNotification
    {
        get => _activePrayerNotification;
        private set
        {
            if (SetProperty(ref _activePrayerNotification, value))
            {
                OnPropertyChanged(nameof(HasActivePrayerNotification));
            }
        }
    }

    // Commands
    public ICommand TogglePlayPauseCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand SeekCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand ToggleBezelMinimizeCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand OpenYouTubeMusicCommand { get; }
    public ICommand OpenTodoCommand { get; }
    public ICommand OpenFloatingBrowserCommand { get; }
    public ICommand ImportLyricsCommand { get; }
    public ICommand OffsetAddCommand { get; }
    public ICommand OffsetSubtractCommand { get; }
    public ICommand OffsetResetCommand { get; }
    public ICommand SearchLyricsCommand { get; }
    public ICommand SelectSearchResultCommand { get; }
    public ICommand SelectSessionCommand { get; }
    public ICommand AddTodoCommand { get; }
    public ICommand ToggleSetDeadlineCommand { get; }
    public ICommand SetDeadlinePresetCommand { get; }
    public ICommand ApplyCustomDeadlineCommand { get; }
    public ICommand ClearNewTodoDueDateCommand { get; }
    public ICommand DismissTodoNotificationCommand { get; }
    public ICommand CompleteTodoNotificationCommand { get; }
    public ICommand ClearCompletedTodosCommand { get; }
    public ICommand SetTodoFilterCommand { get; }
    public ICommand SetCategoryFilterCommand { get; }
    public ICommand MoveCategoryUpCommand { get; }
    public ICommand MoveCategoryDownCommand { get; }
    public ICommand ExpandAllCategoriesCommand { get; }
    public ICommand CollapseAllCategoriesCommand { get; }
    public ICommand ToggleCategoryCollapseCommand { get; }
    public ICommand NextTickerCommand { get; }
    public ICommand PreviousTickerCommand { get; }
    public ICommand CompleteCurrentTickerCommand { get; }
    public ICommand SyncGoogleDriveCommand { get; }
    public ICommand DetachTodoCommand { get; }
    public ICommand AttachTodoCommand { get; }
    public ICommand CloseTodoCommand { get; }
    public ICommand MinimizeTodoWindowCommand { get; }
    public ICommand ExpandTodoWindowCommand { get; }
    public ICommand DetachPrayerCommand { get; }
    public ICommand AttachPrayerCommand { get; }
    public ICommand ClosePrayerCommand { get; }
    public ICommand MinimizePrayerWindowCommand { get; }
    public ICommand ExpandPrayerWindowCommand { get; }
public ICommand TogglePrayerPanelCommand { get; }
    public ICommand PlayAdhanPreviewCommand { get; }
    public ICommand StopAdhanPreviewCommand { get; }
    public ICommand ToggleAdhanPlayPauseCommand { get; }
    public ICommand OpenPrayerScheduleCommand { get; }
    public ICommand DismissPrayerNotificationCommand { get; }
    public ICommand ToggleComponentsMenuCommand { get; }
    public ICommand RestoreAllComponentsCommand { get; }
    public ICommand ToggleBrowserCommand { get; }
    public ICommand OpenBrowserCommand { get; }
    public ICommand CloseBrowserCommand { get; }
    public ICommand OpenRadioCommand { get; }
    public ICommand OpenMusicWindowCommand { get; }
    public ICommand CloseMusicWindowCommand { get; }
    public ICommand MinimizeMusicWindowCommand { get; }
    public ICommand ExpandMusicWindowCommand { get; }
    public ICommand ToggleRadioWindowCommand { get; }
    public ICommand CloseRadioWindowCommand { get; }
    public ICommand MinimizeRadioWindowCommand { get; }
    public ICommand ExpandRadioWindowCommand { get; }
    public ICommand ToggleRadioPlayPauseCommand { get; }
    public ICommand StopRadioCommand { get; }
    public ICommand NextRadioStationCommand { get; }
    public ICommand PreviousRadioStationCommand { get; }
    public ICommand PlayRadioStationCommand { get; }
    public ICommand SetRadioCategoryCommand { get; }
    public ICommand ToggleCurrentRadioFavoriteCommand { get; }
    public ICommand ToggleStationFavoriteCommand { get; }
    public ICommand ToggleRadioMuteCommand { get; }
    public ICommand SearchOnlineRadioCommand { get; }
    public ICommand ToggleAddCustomRadioCommand { get; }
    public ICommand SaveCustomRadioCommand { get; }
    public ICommand DeleteCustomRadioStationCommand { get; }
    public ICommand WakeFromIdleCommand { get; }
    public ICommand ToggleMiniModeCommand { get; }
    public ICommand ToggleVocalRemoverCommand { get; }
    public event Action<bool>? RequestVocalRemoverChanged;
    public ICommand ExitApplicationCommand { get; }

    public event Action? RequestExit;
    public event Action? RequestOpenAbout;

    public MainViewModel(
        IMediaSessionService mediaService,
        ILyricsService lyricsService,
        ISettingsService settingsService,
        ITodoService? todoService = null,
        IGoogleDriveSyncService? googleDriveSyncService = null,
        IUpdateService? updateService = null,
        IPrayerService? prayerService = null,
        IPrayerReminderService? prayerReminderService = null,
        IRadioService? radioService = null)
    {
        _mediaService = mediaService;
        _lyricsService = lyricsService;
        _settingsService = settingsService;
        _isPinned = _settingsService.CurrentSettings.IsPinned;
        _isBezelMinimized = _settingsService.CurrentSettings.IsBezelMinimized;
        _todoService = todoService ?? new TodoService(settingsService);
        _googleDriveSyncService = googleDriveSyncService ?? new GoogleDriveSyncService(settingsService);
        _updateService = updateService ?? new UpdateService(settingsService);
        _prayerService = prayerService ?? new PrayerService(settingsService);
        _prayerReminderService = prayerReminderService ?? new PrayerReminderService(_prayerService, settingsService);
        _radioService = radioService ?? new RadioService(settingsService);

        foreach (var st in _radioService.Stations)
        {
            RadioStations.Add(st);
        }

        _filteredRadioStations = CollectionViewSource.GetDefaultView(RadioStations);
        _filteredRadioStations.Filter = FilterRadioStation;

        _radioService.StateChanged += OnRadioStateChanged;
        _radioService.StationChanged += _ => OnRadioStateChanged();

        TogglePrayerPanelCommand = new RelayCommand(() => IsPrayerPanelOpen = !IsPrayerPanelOpen);
        OpenPrayerScheduleCommand = new RelayCommand(() =>
        {
            if (IsPrayerDetached)
            {
                IsPrayerWindowOpen = true;
                RequestComponentLayoutChanged?.Invoke();
            }
            else
            {
                CurrentMode = PanelMode.Expanded;
                IsPrayerPanelOpen = true;
            }
        });
        DismissPrayerNotificationCommand = new RelayCommand(() =>
        {
            _notificationDismissTimer?.Stop();
            ActivePrayerNotification = null;
        });
        ExitApplicationCommand = new RelayCommand(() => RequestExit?.Invoke());

        _prayerReminderService.ReminderTriggered += OnPrayerReminderTriggered;
        _prayerReminderService.ScheduleUpdated += (sch) =>
        {
            OnPropertyChanged(nameof(CurrentPrayerSchedule));
            OnPropertyChanged(nameof(ShowCompactPrayerPill));
        OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
            OnPropertyChanged(nameof(NextPrayerName));
            OnPropertyChanged(nameof(NextPrayerTimeFormatted));
            OnPropertyChanged(nameof(NextPrayerCountdownBadge));
            OnPropertyChanged(nameof(PrayerScheduleTooltip));
        };

        OpenUpdateCommand = new RelayCommand(async () => await CheckAndInstallUpdateAsync());

        _googleDriveSyncService.StatusChanged += () =>
        {
            void Notify()
            {
                OnPropertyChanged(nameof(IsGoogleDriveConnected));
                OnPropertyChanged(nameof(IsGoogleDriveSyncing));
                OnPropertyChanged(nameof(GoogleDriveUserEmail));
                OnPropertyChanged(nameof(GoogleDriveSyncStatus));
                OnPropertyChanged(nameof(GoogleDriveStatusButtonText));
                OnPropertyChanged(nameof(GoogleDriveSyncTooltip));
            }

            if (System.Windows.Application.Current?.Dispatcher != null &&
                !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(Notify);
            }
            else
            {
                Notify();
            }
        };
        _googleDriveSyncService.TodosSynced += OnTodosSyncedFromCloud;

        _filteredTodos = CollectionViewSource.GetDefaultView(Todos);
        _filteredTodos.Filter = FilterTodoItem;
        _filteredTodos.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TodoItemViewModel.CategoryGroup)));

        var loadedTodos = _todoService.LoadTodos();
        foreach (var item in loadedTodos)
        {
            var vm = new TodoItemViewModel(item);
            vm.OnChanged += OnTodoItemChanged;
            vm.OnDeleteRequested += DeleteTodo;
            Todos.Add(vm);
        }

        AddTodoCommand = new RelayCommand(AddTodo);
        ToggleSetDeadlineCommand = new RelayCommand(() => IsSettingNewTodoDeadline = !IsSettingNewTodoDeadline);
        SetDeadlinePresetCommand = new RelayCommand((p) =>
        {
            if (p is string presetStr)
            {
                SetDeadlinePreset(presetStr);
            }
        });
        ApplyCustomDeadlineCommand = new RelayCommand(ApplyCustomDeadline);
        ClearNewTodoDueDateCommand = new RelayCommand(() =>
        {
            NewTodoDueDate = null;
            IsSettingNewTodoDeadline = false;
        });
        DismissTodoNotificationCommand = new RelayCommand(() =>
        {
            _todoNotificationDismissTimer?.Stop();
            ActiveTodoNotification = null;
        });
        CompleteTodoNotificationCommand = new RelayCommand(() =>
        {
            if (ActiveTodoNotification != null)
            {
                var target = Todos.FirstOrDefault(t => t.Id == ActiveTodoNotification.TodoId);
                if (target != null)
                {
                    target.IsCompleted = true;
                }
                _todoNotificationDismissTimer?.Stop();
                ActiveTodoNotification = null;
            }
        });
        _todoDeadlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _todoDeadlineTimer.Tick += (s, e) => CheckTodoDeadlines();
        try
        {
            _todoDeadlineTimer.Start();
        }
        catch { }
        ClearCompletedTodosCommand = new RelayCommand(ClearCompletedTodos);
        SetTodoFilterCommand = new RelayCommand((f) =>
        {
            if (f is string filterStr)
            {
                TodoFilter = filterStr;
            }
        });

        SetCategoryFilterCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                SelectedCategoryFilter = cat;
                if (!string.Equals(cat, "All", StringComparison.OrdinalIgnoreCase))
                {
                    NewTodoCategory = cat;
                }
            }
        });

        MoveCategoryUpCommand = new RelayCommand(p => MoveCategoryUp(p));
        MoveCategoryDownCommand = new RelayCommand(p => MoveCategoryDown(p));
        ExpandAllCategoriesCommand = new RelayCommand(ExpandAllCategories);
        CollapseAllCategoriesCommand = new RelayCommand(CollapseAllCategories);
        ToggleCategoryCollapseCommand = new RelayCommand(p => ToggleCategoryCollapse(p));

        UpdateAvailableCategories();

        NextTickerCommand = new RelayCommand(NextTicker);
        PreviousTickerCommand = new RelayCommand(PreviousTicker);
        CompleteCurrentTickerCommand = new RelayCommand(CompleteCurrentTicker);
        SyncGoogleDriveCommand = new RelayCommand(async () => await HandleGoogleDriveClickAsync());

        UpdateTicker();

        TogglePlayPauseCommand = new RelayCommand(async () => await _mediaService.TogglePlayPauseAsync());
        PreviousCommand = new RelayCommand(async () => await _mediaService.PreviousAsync());
        NextCommand = new RelayCommand(async () => await _mediaService.NextAsync());
        SeekCommand = new RelayCommand(async (param) =>
        {
            if (param is double seconds)
            {
                await _mediaService.SeekAsync(TimeSpan.FromSeconds(seconds));
            }
        });

        SetModeCommand = new RelayCommand((modeObj) =>
        {
            if (modeObj is PanelMode mode)
            {
                if (mode == PanelMode.Expanded) IsPrayerPanelOpen = false;
                CurrentMode = mode;
            }
            else if (modeObj is string modeStr && Enum.TryParse<PanelMode>(modeStr, true, out var parsed))
            {
                if (parsed == PanelMode.Expanded) IsPrayerPanelOpen = false;
                CurrentMode = parsed;
            }
        });

        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        ToggleBezelMinimizeCommand = new RelayCommand(() => IsBezelMinimized = !IsBezelMinimized);
        WakeFromIdleCommand = new RelayCommand(WakeFromIdle);
        ToggleMiniModeCommand = new RelayCommand(ToggleMiniMode);
        OpenSettingsCommand = new RelayCommand(() => RequestOpenSettings?.Invoke());
        OpenAboutCommand = new RelayCommand(() => RequestOpenAbout?.Invoke());
        OpenYouTubeMusicCommand = new RelayCommand(() => RequestOpenYouTubeMusic?.Invoke());
        ToggleVocalRemoverCommand = new RelayCommand(() => IsVocalRemoverEnabled = !IsVocalRemoverEnabled);
        OpenTodoCommand = new RelayCommand(() =>
        {
            CurrentMode = PanelMode.Compact;
            IsTodoWindowCompact = false;
            IsTodoDetached = true;
            if (IsTodoDetached)
            {
                IsTodoWindowOpen = true;
                RequestComponentLayoutChanged?.Invoke();
            }
            else
            {
                CurrentMode = PanelMode.Expanded;
            }
        });
        OpenFloatingBrowserCommand = new RelayCommand(() => RequestOpenFloatingBrowser?.Invoke());
        ImportLyricsCommand = new RelayCommand(ImportLocalLyrics);
        OffsetAddCommand = new RelayCommand(() => AdjustOffset(0.5));
        OffsetSubtractCommand = new RelayCommand(() => AdjustOffset(-0.5));
        OffsetResetCommand = new RelayCommand(() => AdjustOffset(0, reset: true));
        SearchLyricsCommand = new RelayCommand(async () => await ExecuteManualSearchAsync());
        SelectSearchResultCommand = new RelayCommand((item) =>
        {
            if (item is LrcLibSearchResult res)
            {
                ApplySearchResult(res);
            }
        });
        SelectSessionCommand = new RelayCommand((sessId) =>
        {
            if (sessId is string id)
            {
                _mediaService.SelectSession(id);
                _settingsService.CurrentSettings.PreferredSessionId = id;
                _settingsService.Save();
            }
        });

        DetachTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = true;
            IsTodoWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        AttachTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = false;
            IsTodoWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        CloseTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = true;
            IsTodoWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeTodoWindowCommand = new RelayCommand(() => IsTodoWindowCompact = true);
        ExpandTodoWindowCommand = new RelayCommand(() => IsTodoWindowCompact = false);

        DetachPrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = true;
            IsPrayerWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        AttachPrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = false;
            IsPrayerWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        ClosePrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = true;
            IsPrayerWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizePrayerWindowCommand = new RelayCommand(() => IsPrayerWindowCompact = true);
ExpandPrayerWindowCommand = new RelayCommand(() => IsPrayerWindowCompact = false);

        PlayAdhanPreviewCommand = new RelayCommand(() =>
        {
            if (IsAdhanPlaying)
            {
                PrayerAudioHelper.StopAudio();
                IsAdhanPlaying = false;
            }
            else
            {
                IsAdhanPlaying = true;
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, Settings.CustomAdhanSoundPath, Settings.AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        StopAdhanPreviewCommand = new RelayCommand(() =>
        {
            PrayerAudioHelper.StopAudio();
            IsAdhanPlaying = false;
        });

        ToggleAdhanPlayPauseCommand = new RelayCommand(() =>
        {
            if (PrayerAudioHelper.IsPlaying && !PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.PauseAudio();
            }
            else if (PrayerAudioHelper.IsPlaying && PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.ResumeAudio();
            }
            else
            {
                IsAdhanPlaying = true;
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, Settings.CustomAdhanSoundPath, Settings.AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        PrayerAudioHelper.PlaybackProgressChanged += (current, total) =>
        {
            _adhanCurrentSec = current;
            _adhanTotalSec = total;
            OnPropertyChanged(nameof(AdhanCurrentPositionSeconds));
            OnPropertyChanged(nameof(AdhanProgressText));
            OnPropertyChanged(nameof(AdhanProgressPercent));
            OnPropertyChanged(nameof(AdhanCurrentPositionText));
            OnPropertyChanged(nameof(AdhanTotalDurationText));
        };

        PrayerAudioHelper.PlaybackStateChanged += () =>
        {
            _isAdhanPlaying = PrayerAudioHelper.IsPlaying;
            _isAdhanPaused = PrayerAudioHelper.IsPaused;
            OnPropertyChanged(nameof(IsAdhanPlaying));
            OnPropertyChanged(nameof(IsAdhanPaused));
            OnPropertyChanged(nameof(AdhanPlayButtonIcon));
            OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
        };

        ToggleComponentsMenuCommand = new RelayCommand(() => IsComponentsMenuOpen = !IsComponentsMenuOpen);
        RestoreAllComponentsCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = false;
            IsPrayerWindowOpen = true;
            IsTodoDetached = false;
            IsTodoWindowOpen = true;
            IsBrowserWindowOpen = true;
            ComponentDisplayMode = "Both";
            IsComponentsMenuOpen = false;
        });

        ToggleBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = !IsBrowserWindowOpen;
            IsComponentsMenuOpen = false;
        });
        OpenBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        CloseBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = false;
            IsComponentsMenuOpen = false;
        });

        OpenRadioCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenRadio?.Invoke();
        });
        OpenMusicWindowCommand = new RelayCommand(() =>
        {
            IsMusicWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenMusic?.Invoke();
        });
        CloseMusicWindowCommand = new RelayCommand(() =>
        {
            IsMusicWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        ToggleRadioWindowCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = !IsRadioWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsRadioWindowOpen)
            {
                RequestOpenRadio?.Invoke();
            }
        });
        CloseRadioWindowCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeRadioWindowCommand = new RelayCommand(() => IsRadioWindowCompact = true);
        MinimizeMusicWindowCommand = new RelayCommand(() => IsMusicWindowCompact = true);
        ExpandMusicWindowCommand = new RelayCommand(() => IsMusicWindowCompact = false);
        ExpandRadioWindowCommand = new RelayCommand(() => IsRadioWindowCompact = false);

        ToggleRadioPlayPauseCommand = new RelayCommand(() => _radioService.TogglePlayPause());
        StopRadioCommand = new RelayCommand(() => _radioService.Stop());
        NextRadioStationCommand = new RelayCommand(() => _radioService.NextStation(FilteredRadioStations.Cast<RadioStation>()));
        PreviousRadioStationCommand = new RelayCommand(() => _radioService.PreviousStation(FilteredRadioStations.Cast<RadioStation>()));
        PlayRadioStationCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st)
            {
                _radioService.PlayStation(st);
            }
        });
        SetRadioCategoryCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                RadioCategoryFilter = cat;
            }
        });
        ToggleCurrentRadioFavoriteCommand = new RelayCommand(() =>
        {
            if (_radioService.CurrentStation != null)
            {
                _radioService.ToggleFavorite(_radioService.CurrentStation);
                _filteredRadioStations?.Refresh();
            }
        });
        ToggleStationFavoriteCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st)
            {
                _radioService.ToggleFavorite(st);
                _filteredRadioStations?.Refresh();
            }
        });
        ToggleRadioMuteCommand = new RelayCommand(() => IsRadioMuted = !IsRadioMuted);
        SearchOnlineRadioCommand = new RelayCommand(async () => await ExecuteOnlineRadioSearchAsync());
        ToggleAddCustomRadioCommand = new RelayCommand(() => IsAddingCustomRadio = !IsAddingCustomRadio);
        SaveCustomRadioCommand = new RelayCommand(SaveCustomRadioStation);
        DeleteCustomRadioStationCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st && st.IsCustom)
            {
                _radioService.RemoveCustomStation(st);
                RadioStations.Remove(st);
                _filteredRadioStations?.Refresh();
            }
        });

        _mediaService.TrackChanged += OnTrackChanged;
        _mediaService.TimelineUpdated += OnTimelineUpdated;
        _mediaService.SessionsChanged += (s, list) =>
        {
            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(AvailableSessions)));
            }
            else
            {
                OnPropertyChanged(nameof(AvailableSessions));
            }
        };

        // Smooth timer for progress and lyrics interpolation (runs at ~25fps)
        _interpolationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _interpolationTimer.Tick += InterpolationTimer_Tick;
        _interpolationTimer.Start();

        StartCloudSyncPollingTimer();
    }

    private DispatcherTimer? _cloudSyncPollingTimer;

    private void StartCloudSyncPollingTimer()
    {
        if (System.Windows.Application.Current?.Dispatcher == null) return;

        _cloudSyncPollingTimer?.Stop();
        _cloudSyncPollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _cloudSyncPollingTimer.Tick += async (s, e) =>
        {
            try
            {
                if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
                {
                    await _googleDriveSyncService.CheckForRemoteChangesAsync();
                }
            }
            catch { }
        };
        _cloudSyncPollingTimer.Start();
    }

    public void RefreshComponentLayout()
    {
        OnPropertyChanged(nameof(ComponentDisplayMode));
        OnPropertyChanged(nameof(IsTodoDetached));
        OnPropertyChanged(nameof(IsPrayerDetached));
        OnPropertyChanged(nameof(IsTodoWindowOpen));
        OnPropertyChanged(nameof(IsPrayerWindowOpen));
        OnPropertyChanged(nameof(IsPrayerClosed));
        OnPropertyChanged(nameof(IsTodoClosed));
        OnPropertyChanged(nameof(IsTodoDetachedWindowVisible));
        OnPropertyChanged(nameof(HasClosedComponents));
        OnPropertyChanged(nameof(ClosedComponentsCount));
        OnPropertyChanged(nameof(ComponentsButtonText));
        OnPropertyChanged(nameof(ComponentsButtonSymbol));
        OnPropertyChanged(nameof(ComponentsButtonTooltip));
        OnPropertyChanged(nameof(IsComponentBoth));
        OnPropertyChanged(nameof(IsComponentMusicOnly));
        OnPropertyChanged(nameof(IsComponentTodoOnly));
        OnPropertyChanged(nameof(ShowMusicSection));
        OnPropertyChanged(nameof(ShowTodoSection));
        OnPropertyChanged(nameof(ShowTodoTicker));
        OnPropertyChanged(nameof(ShowTodoSectionInMainWindow));
        OnPropertyChanged(nameof(ShowMusicMinimizeButton));
        OnPropertyChanged(nameof(ShowSplitter));
        OnPropertyChanged(nameof(ShowIdleMiniView));
        OnPropertyChanged(nameof(ShowCompactMusicView));
        OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
        OnPropertyChanged(nameof(ShowPrayerDivider1));
        OnPropertyChanged(nameof(ShowPrayerDivider2));
        OnPropertyChanged(nameof(ShowPrayerSectionInMainWindow));
        OnPropertyChanged(nameof(ShowExpandedPrayerDivider1));
        OnPropertyChanged(nameof(ShowExpandedPrayerDivider2));
        OnPropertyChanged(nameof(MusicColumnWidth));
        OnPropertyChanged(nameof(PrayerColumnWidth));
        OnPropertyChanged(nameof(TodoColumnWidth));
        OnPropertyChanged(nameof(ExpandedPanelWidth));
        OnPropertyChanged(nameof(EnablePrayerTimes));
        if (!EnablePrayerTimes) ActivePrayerNotification = null;
        OnPropertyChanged(nameof(HasActivePrayerNotification));
        OnPropertyChanged(nameof(ShowCompactPrayerPill));
        OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
        OnPropertyChanged(nameof(CurrentPrayerSchedule));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTimeFormatted));
        OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        OnPropertyChanged(nameof(PrayerScheduleTooltip));
        OnPropertyChanged(nameof(IsRadioWindowOpen));
        OnPropertyChanged(nameof(IsRadioActiveOrOpen));
        OnPropertyChanged(nameof(RadioStatusSummary));
        OnPropertyChanged(nameof(RadioButtonActionText));
        RefreshAdhanSettings();
        _prayerReminderService.RefreshSchedule();
        RequestComponentLayoutChanged?.Invoke();
    }

    public void RefreshAdhanSettings()
    {
        OnPropertyChanged(nameof(SelectedAdhanSound));
        OnPropertyChanged(nameof(SelectedAdhanSoundName));
        OnPropertyChanged(nameof(AdhanPlayButtonIcon));
        OnPropertyChanged(nameof(AdhanPlayButtonTooltip));
        OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
    }

    private void OnRadioStateChanged()
    {
        Action notify = () =>
        {
            OnPropertyChanged(nameof(CurrentRadioStation));
            OnPropertyChanged(nameof(CurrentRadioStationName));
            OnPropertyChanged(nameof(CurrentRadioStationSubtitle));
            OnPropertyChanged(nameof(CurrentRadioFavoriteSymbol));
            OnPropertyChanged(nameof(IsRadioPlaying));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsRadioBuffering));
            OnPropertyChanged(nameof(IsRadioActiveOrOpen));
            OnPropertyChanged(nameof(ShowFullRadioInCompact));
            OnPropertyChanged(nameof(IsNotchActive));
            OnPropertyChanged(nameof(RadioStateBadgeText));
            OnPropertyChanged(nameof(RadioStatusMessage));
            OnPropertyChanged(nameof(RadioCompactLabel));
            OnPropertyChanged(nameof(RadioNotchTooltip));
            OnPropertyChanged(nameof(RadioStatusSummary));
            OnPropertyChanged(nameof(RadioVolume));
            OnPropertyChanged(nameof(IsRadioMuted));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
        };

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(notify);
        }
        else
        {
            notify();
        }
    }

    private bool FilterRadioStation(object obj)
    {
        if (obj is not RadioStation st) return false;

        if (!string.IsNullOrWhiteSpace(_radioCategoryFilter) && !string.Equals(_radioCategoryFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(_radioCategoryFilter, "Favorites", StringComparison.OrdinalIgnoreCase))
            {
                if (!st.IsFavorite) return false;
            }
            else if (!string.Equals(st.Category, _radioCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_radioSearchQuery))
        {
            var q = _radioSearchQuery.Trim();
            bool match = st.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.CityOrCountry.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.Genre.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!match) return false;
        }

        return true;
    }

    private async Task ExecuteOnlineRadioSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(RadioSearchQuery)) return;

        _radioSearchCts?.Cancel();
        _radioSearchCts?.Dispose();
        _radioSearchCts = new CancellationTokenSource();
        var token = _radioSearchCts.Token;

        IsSearchingOnlineRadio = true;
        try
        {
            var results = await _radioService.SearchOnlineStationsAsync(RadioSearchQuery, token);
            if (!token.IsCancellationRequested && results.Count > 0)
            {
                RadioCategoryFilter = "All";
                foreach (var r in results)
                {
                    if (!RadioStations.Any(existing => string.Equals(existing.Id, r.Id, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(existing.StreamUrl, r.StreamUrl, StringComparison.OrdinalIgnoreCase)))
                    {
                        RadioStations.Add(r);
                    }
                }
                _filteredRadioStations?.Refresh();
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            IsSearchingOnlineRadio = false;
        }
    }

    private void SaveCustomRadioStation()
    {
        if (string.IsNullOrWhiteSpace(CustomRadioName) || string.IsNullOrWhiteSpace(CustomRadioUrl))
            return;

        var url = CustomRadioUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var st = _radioService.AddCustomStation(CustomRadioName, url);
        if (!RadioStations.Contains(st))
        {
            RadioStations.Add(st);
        }

        CustomRadioName = string.Empty;
        CustomRadioUrl = string.Empty;
        IsAddingCustomRadio = false;
        RadioCategoryFilter = "All";
        _filteredRadioStations?.Refresh();
        _radioService.PlayStation(st);
    }

    private void OnPrayerReminderTriggered(PrayerNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActivePrayerNotification = evt;
            RequestShowTrayNotification?.Invoke(evt.Title, evt.Message);

            if (evt.AutoPauseRequested)
            {
                if (IsPlaying)
                {
                    _ = _mediaService.PauseAsync();
                }
                if (IsRadioPlaying || IsRadioBuffering)
                {
                    _radioService.Stop();
                }
            }

            // Auto dismiss notification banner after 20 seconds
            _notificationDismissTimer?.Stop();
            _notificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _notificationDismissTimer.Tick += (s, e) =>
            {
                _notificationDismissTimer?.Stop();
                ActivePrayerNotification = null;
            };
            _notificationDismissTimer.Start();
        });
    }

    public async Task InitializeAsync()
    {
        await _mediaService.InitializeAsync();
        _prayerReminderService.Start();

        if (!IsPinned && !_isHovering)
        {
            ResetIdleTimer();
        }

        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync)
        {
            _ = Task.Run(async () =>
            {
                await SyncWithGoogleDriveAsync();
            });
        }

        if (Settings.AutoCheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000);
                    var update = await _updateService.CheckForUpdatesAsync();
                    if (update != null && update.IsNewerVersion)
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            AvailableUpdateInfo = update;
                            AvailableUpdateVersion = $"v{update.Version}";
                            HasAvailableUpdate = true;
                        });
                    }
                }
                catch { }
            });
        }
    }

    public async Task CheckAndInstallUpdateAsync()
    {
        if (_isCheckingOrInstallingUpdate) return;
        _isCheckingOrInstallingUpdate = true;

        try
        {
            if (!HasAvailableUpdate || AvailableUpdateInfo == null)
            {
                UpdateActionStatusText = "Mengecek...";
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null && update.IsNewerVersion)
                {
                    AvailableUpdateInfo = update;
                    AvailableUpdateVersion = $"v{update.Version.TrimStart('v', 'V')}";
                    HasAvailableUpdate = true;
                }
                else if (update != null)
                {
                    UpdateActionStatusText = "Sudah terbaru ✓";
                    RequestShowTrayNotification?.Invoke("Pembaruan DRANOTCH", $"Aplikasi sudah menggunakan versi terbaru ({CurrentVersionText}).");
                    await Task.Delay(2200);
                    UpdateActionStatusText = string.Empty;
                    return;
                }
                else
                {
                    UpdateActionStatusText = "Gagal cek";
                    await Task.Delay(2200);
                    UpdateActionStatusText = string.Empty;
                    return;
                }
            }

            if (AvailableUpdateInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(AvailableUpdateInfo.DownloadUrl))
                {
                    UpdateActionStatusText = "Unduh 0%";
                    var progress = new Progress<double>(pct =>
                    {
                        UpdateActionStatusText = $"Unduh {pct:0}%";
                    });
                    var installerPath = await _updateService.DownloadUpdateAsync(AvailableUpdateInfo.DownloadUrl, progress);
                    UpdateActionStatusText = "Menginstal...";
                    await Task.Delay(400);
                    _updateService.LaunchInstallerAndExit(installerPath);
                }
                else if (!string.IsNullOrWhiteSpace(AvailableUpdateInfo.ReleasePageUrl))
                {
                    UpdateActionStatusText = string.Empty;
                    try
                    {
                        Process.Start(new ProcessStartInfo(AvailableUpdateInfo.ReleasePageUrl) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            UpdateActionStatusText = "Gagal update";
            RequestShowTrayNotification?.Invoke("Pembaruan DRANOTCH", $"Gagal mengunduh pembaruan: {ex.Message}");
            await Task.Delay(2200);
            UpdateActionStatusText = string.Empty;
        }
        finally
        {
            _isCheckingOrInstallingUpdate = false;
        }
    }

    private void OnTrackChanged(object? sender, TrackInfo? track)
    {
        Action update = () =>
        {
            var oldTrackKey = _currentTrack?.UniqueSongKey;
            CurrentTrack = track;

            if (track == null || string.IsNullOrWhiteSpace(track.Title))
            {
                SessionStatus = "Tidak ada lagu yang sedang diputar";
                ActiveLyric = string.Empty;
                PreviousLyric = string.Empty;
                NextLyric = string.Empty;
                DurationSeconds = 0;
                CurrentPositionSeconds = 0;
                FormattedTime = "0:00 / 0:00";
                _playbackStopwatch.Reset();
                return;
            }

            SessionStatus = $"{track.SourceAppId}";
            DurationSeconds = track.Duration.TotalSeconds;
            _baseTrackPosition = track.Position;
            _playbackStopwatch.Restart();

            if (track.UniqueSongKey != oldTrackKey)
            {
                _isLocalLyricsLoaded = false;
                _activeLyricIndex = -1;

                // Load saved offset for song
                if (Settings.SongOffsetsSeconds.TryGetValue(track.UniqueSongKey, out var savedOffset))
                {
                    ManualOffsetSeconds = savedOffset;
                }
                else
                {
                    ManualOffsetSeconds = 0;
                }

                FetchLyricsForTrack(track);
            }

            UpdateTimelineAndLyrics();
        };

        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(update);
        }
        else
        {
            update();
        }
    }

    private void OnTimelineUpdated(object? sender, TimeSpan position)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            _baseTrackPosition = position;
            _playbackStopwatch.Restart();
            UpdateTimelineAndLyrics();
        });
    }

    private void InterpolationTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentTrack == null) return;

        UpdateTimelineAndLyrics();
    }

    private void UpdateTimelineAndLyrics()
    {
        if (CurrentTrack == null) return;

        TimeSpan currentPos;
        if (CurrentTrack.IsPlaying)
        {
            var elapsed = _playbackStopwatch.Elapsed * CurrentTrack.PlaybackRate;
            currentPos = _baseTrackPosition + elapsed;
            if (CurrentTrack.Duration > TimeSpan.Zero && currentPos > CurrentTrack.Duration)
            {
                currentPos = CurrentTrack.Duration;
            }
        }
        else
        {
            currentPos = _baseTrackPosition;
        }

        CurrentPositionSeconds = currentPos.TotalSeconds;
        DurationSeconds = CurrentTrack.Duration.TotalSeconds;

        var posStr = FormatTimeSpan(currentPos);
        var durStr = FormatTimeSpan(CurrentTrack.Duration);
        FormattedTime = $"{posStr} / {durStr}";

        // Update Lyrics
        if (CurrentLyrics == null)
        {
            ActiveLyric = "Memuat lirik...";
            PreviousLyric = string.Empty;
            NextLyric = string.Empty;
            return;
        }

        if (!CurrentLyrics.IsSynced)
        {
            ActiveLyric = CurrentLyrics.PlainLyrics;
            PreviousLyric = string.Empty;
            NextLyric = string.Empty;
            return;
        }

        var effectivePos = currentPos + TimeSpan.FromSeconds(ManualOffsetSeconds);
        var idx = LrcParser.GetActiveLineIndex(CurrentLyrics.Lines, effectivePos);

        if (idx != _activeLyricIndex)
        {
            _activeLyricIndex = idx;
            if (idx >= 0 && idx < CurrentLyrics.Lines.Count)
            {
                ActiveLyric = CurrentLyrics.Lines[idx].Text;
                PreviousLyric = idx > 0 ? CurrentLyrics.Lines[idx - 1].Text : string.Empty;
                NextLyric = idx < CurrentLyrics.Lines.Count - 1 ? CurrentLyrics.Lines[idx + 1].Text : string.Empty;
            }
            else
            {
                ActiveLyric = "♪ ♪ ♪";
                PreviousLyric = string.Empty;
                NextLyric = CurrentLyrics.Lines.Count > 0 ? CurrentLyrics.Lines[0].Text : string.Empty;
            }
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return ts.ToString(@"h\:mm\:ss");
        }
        return ts.ToString(@"m\:ss");
    }

    private void FetchLyricsForTrack(TrackInfo track)
    {
        _lyricsCts?.Cancel();
        _lyricsCts?.Dispose();
        _lyricsCts = new CancellationTokenSource();
        var token = _lyricsCts.Token;

        ActiveLyric = "Mencari lirik di LRCLIB...";
        PreviousLyric = string.Empty;
        NextLyric = string.Empty;

        if (!Settings.AutoSearchLyrics)
        {
            CurrentLyrics = SongLyrics.Empty("Pencarian otomatis dinonaktifkan di Pengaturan");
            ActiveLyric = CurrentLyrics.PlainLyrics;
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var result = await _lyricsService.GetLyricsAsync(track.Title, track.Artist, track.Album, track.Duration, token);
                if (!token.IsCancellationRequested)
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        // Check if track is still the same and user hasn't imported local lyrics in the meantime
                        if (!_isLocalLyricsLoaded && CurrentTrack?.UniqueSongKey == track.UniqueSongKey)
                        {
                            CurrentLyrics = result;
                            _activeLyricIndex = -1;
                            UpdateTimelineAndLyrics();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    if (!_isLocalLyricsLoaded && CurrentTrack?.UniqueSongKey == track.UniqueSongKey)
                    {
                        CurrentLyrics = SongLyrics.Empty($"Gagal mengambil lirik: {ex.Message}");
                        ActiveLyric = CurrentLyrics.PlainLyrics;
                    }
                });
            }
        }, token);
    }

    private void ImportLocalLyrics()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "File Lirik (*.lrc;*.txt)|*.lrc;*.txt|LRC Files (*.lrc)|*.lrc|Text Files (*.txt)|*.txt",
            Title = "Pilih File Lirik Lokal"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _lyricsCts?.Cancel(); // Cancel any ongoing online fetch
                var loaded = _lyricsService.LoadFromLocalFile(dialog.FileName);
                _isLocalLyricsLoaded = true;
                CurrentLyrics = loaded;
                _activeLyricIndex = -1;
                UpdateTimelineAndLyrics();
            }
            catch (Exception ex)
            {
                ActiveLyric = $"Gagal membaca file: {ex.Message}";
            }
        }
    }

    private void AdjustOffset(double deltaSeconds, bool reset = false)
    {
        if (reset)
        {
            ManualOffsetSeconds = 0;
        }
        else
        {
            ManualOffsetSeconds = Math.Round(ManualOffsetSeconds + deltaSeconds, 1);
        }

        if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.UniqueSongKey))
        {
            Settings.SongOffsetsSeconds[CurrentTrack.UniqueSongKey] = ManualOffsetSeconds;
            _settingsService.Save();
        }

        _activeLyricIndex = -1; // force refresh
        UpdateTimelineAndLyrics();
    }

    private async Task ExecuteManualSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        try
        {
            var results = await _lyricsService.SearchCandidatesAsync(SearchQuery);
            SearchResults = results;
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void ApplySearchResult(LrcLibSearchResult item)
    {
        _lyricsCts?.Cancel();
        _isLocalLyricsLoaded = true; // Protect from auto-fetch overrides

        if (item.Instrumental == true)
        {
            CurrentLyrics = new SongLyrics
            {
                Title = item.TrackName ?? "",
                Artist = item.ArtistName ?? "",
                IsSynced = false,
                PlainLyrics = "♪ Instrumental ♪",
                Source = "LRCLIB Manual Selection"
            };
        }
        else if (!string.IsNullOrWhiteSpace(item.SyncedLyrics))
        {
            var lrc = LrcParser.Parse(item.SyncedLyrics);
            lrc.Title = item.TrackName ?? "";
            lrc.Artist = item.ArtistName ?? "";
            lrc.Source = "LRCLIB Manual (Synced)";
            CurrentLyrics = lrc;
        }
        else if (!string.IsNullOrWhiteSpace(item.PlainLyrics))
        {
            CurrentLyrics = new SongLyrics
            {
                Title = item.TrackName ?? "",
                Artist = item.ArtistName ?? "",
                IsSynced = false,
                PlainLyrics = item.PlainLyrics,
                Source = "LRCLIB Manual (Plain)"
            };
        }

        SearchResults = new List<LrcLibSearchResult>();
        _activeLyricIndex = -1;
        UpdateTimelineAndLyrics();
    }

    private bool FilterTodoItem(object obj)
    {
        if (obj is not TodoItemViewModel item) return false;

        // 1. Status Filter
        bool statusMatch = _todoFilter switch
        {
            "Active" => !item.IsCompleted,
            "Completed" => item.IsCompleted,
            _ => true
        };
        if (!statusMatch) return false;

        // 2. Category Filter
        if (!string.Equals(SelectedCategoryFilter, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(SelectedCategoryFilter))
        {
            if (!string.Equals(item.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void UpdateAvailableCategories()
    {
        var cats = Todos
            .Where(t => t.HasCategory)
            .Select(t => t.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var previousSelected = SelectedCategoryFilter;

        AvailableCategories.Clear();
        AvailableCategories.Add("All");
        foreach (var c in cats)
        {
            AvailableCategories.Add(c);
        }

        ExistingCategories.Clear();
        ExistingCategories.Add("(Tanpa Kategori)");
        foreach (var c in cats)
        {
            ExistingCategories.Add(c);
        }

        if (AvailableCategories.Contains(previousSelected, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCategoryFilter = previousSelected;
        }
        else
        {
            SelectedCategoryFilter = "All";
        }

        OnPropertyChanged(nameof(HasCategories));
    }

    private void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;

        var rawTitle = NewTodoTitle.Trim();
        string titleToSave;
        string categoryToSave = string.Empty;

        var (parsedCat, _) = TodoItem.ParseCategory(rawTitle);
        if (!string.IsNullOrEmpty(parsedCat))
        {
            categoryToSave = parsedCat;
            titleToSave = rawTitle;
        }
        else if (!string.IsNullOrWhiteSpace(NewTodoCategory) &&
                 !NewTodoCategory.Trim().Equals("(Tanpa Kategori)", StringComparison.OrdinalIgnoreCase))
        {
            categoryToSave = NewTodoCategory.Trim();
            titleToSave = $"{categoryToSave}: {rawTitle}";
        }
        else
        {
            titleToSave = rawTitle;
        }

        var item = new TodoItem
        {
            Title = titleToSave,
            Category = categoryToSave,
            CreatedAt = DateTime.Now,
            DueDate = NewTodoDueDate,
            IsDueNotified = false
        };

        var vm = new TodoItemViewModel(item);
        vm.OnChanged += OnTodoItemChanged;
        vm.OnDeleteRequested += DeleteTodo;

        Todos.Add(vm);
        NewTodoTitle = string.Empty;
        NewTodoDueDate = null;
        IsSettingNewTodoDeadline = false;

        UpdateTodoCountsAndPersist();
    }

    public void SetDeadlinePreset(string preset)
    {
        var now = DateTime.Now;
        DateTime target;
        switch (preset?.ToLowerInvariant())
        {
            case "plus_1h":
                target = now.AddHours(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "plus_3h":
                target = now.AddHours(3);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "today_17":
                NewTodoDueDate = new DateTime(now.Year, now.Month, now.Day, 17, 0, 0);
                break;
            case "today_21":
                NewTodoDueDate = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);
                break;
            case "tomorrow_09":
                target = now.AddDays(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, 9, 0, 0);
                break;
            case "tomorrow_17":
                target = now.AddDays(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, 17, 0, 0);
                break;
            default:
                break;
        }
        IsSettingNewTodoDeadline = false;
    }

    public void ApplyCustomDeadline()
    {
        var baseDate = CustomDeadlineDate ?? DateTime.Today;
        int hour = Math.Clamp(CustomDeadlineHour, 0, 23);
        int minute = Math.Clamp(CustomDeadlineMinute, 0, 59);
        NewTodoDueDate = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, 0);
        IsSettingNewTodoDeadline = false;
    }

    public void CheckTodoDeadlines()
    {
        var now = DateTime.Now;
        bool hasChanges = false;

        foreach (var vm in Todos)
        {
            if (vm.HasDueDate)
            {
                vm.RefreshDueStatus();
            }

            if (!vm.IsCompleted && vm.DueDate.HasValue && !vm.IsDueNotified && now >= vm.DueDate.Value)
            {
                vm.IsDueNotified = true;
                hasChanges = true;

                // 1. Play alert chime sound
                PrayerAudioHelper.PlayDeadlineAlert();

                // 2. Windows tray notification balloon
                RequestShowTrayNotification?.Invoke("⏰ Batas Waktu To-Do!", $"Tugas '{vm.TaskText}' telah mencapai batas waktu.");

                // 3. Notch Floating Island Banner
                ActiveTodoNotification = new TodoNotificationEvent
                {
                    TodoId = vm.Id,
                    Title = "⏰ Batas Waktu To-Do!",
                    Message = $"Tugas '{vm.TaskText}' telah mencapai batas waktu ({vm.DueDateText}).",
                    Timestamp = now
                };
            }
        }

        if (hasChanges)
        {
            UpdateTodoCountsAndPersist();
        }
    }

    private void DeleteTodo(TodoItemViewModel item)
    {
        item.OnChanged -= OnTodoItemChanged;
        item.OnDeleteRequested -= DeleteTodo;
        Todos.Remove(item);
        _todoService.MarkDeleted(item.Id);
        UpdateTodoCountsAndPersist();
    }

    private void ClearCompletedTodos()
    {
        var completed = Todos.Where(t => t.IsCompleted).ToList();
        if (completed.Count == 0) return;

        var ids = completed.Select(t => t.Id).ToList();
        foreach (var item in completed)
        {
            item.OnChanged -= OnTodoItemChanged;
            item.OnDeleteRequested -= DeleteTodo;
            Todos.Remove(item);
        }
        _todoService.MarkDeleted(ids);
        UpdateTodoCountsAndPersist();
    }

    private void OnTodoItemChanged()
    {
        UpdateTodoCountsAndPersist();
    }

    private void UpdateTodoCountsAndPersist()
    {
        OnPropertyChanged(nameof(PendingTodosCount));
        OnPropertyChanged(nameof(CompletedTodosCount));
        OnPropertyChanged(nameof(TotalTodosCount));
        OnPropertyChanged(nameof(HasCompletedTodos));
        OnPropertyChanged(nameof(HasPendingTodos));
        OnPropertyChanged(nameof(TodoSummaryText));
        UpdateAvailableCategories();
        _todoService.SaveTodos(Todos.Select(t => t.ToModel()));
        _filteredTodos?.Refresh();
        UpdateTicker();
        ScheduleAutoSync();
    }

    private readonly object _autoSyncLock = new();
    private bool _isAutoSyncRunning = false;
    private bool _hasPendingAutoSync = false;

    private void ScheduleAutoSync()
    {
        if (!_googleDriveSyncService.IsConnected || !Settings.GoogleDriveAutoSync) return;

        lock (_autoSyncLock)
        {
            if (_isAutoSyncRunning)
            {
                _hasPendingAutoSync = true;
                return;
            }
            _isAutoSyncRunning = true;
            _hasPendingAutoSync = false;
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                List<TodoItem> snapshot;
                if (System.Windows.Application.Current?.Dispatcher != null &&
                    !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    snapshot = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => Todos.Select(t => t.ToModel()).ToList());
                }
                else
                {
                    snapshot = Todos.Select(t => t.ToModel()).ToList();
                }

                try
                {
                    if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync)
                    {
                        await _googleDriveSyncService.UploadTodosAsync(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoSync error: {ex.Message}");
                }

                lock (_autoSyncLock)
                {
                    if (_hasPendingAutoSync)
                    {
                        _hasPendingAutoSync = false;
                        continue;
                    }
                    _isAutoSyncRunning = false;
                    break;
                }
            }
        });
    }

    public async Task HandleGoogleDriveClickAsync()
    {
        if (!_googleDriveSyncService.IsConnected)
        {
            try
            {
                RequestShowTrayNotification?.Invoke("Google Drive", "Membuka browser untuk login akun Google...");
                var success = await _googleDriveSyncService.AuthenticateAsync();
                if (success)
                {
                    RequestShowTrayNotification?.Invoke("Google Drive", $"Berhasil terhubung sebagai {_googleDriveSyncService.UserEmail}!");
                    await SyncWithGoogleDriveAsync();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Tidak dapat login ke Google Drive:\n{_googleDriveSyncService.SyncStatus}",
                        "DRANOTCH - Google Drive",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Gagal otentikasi Google Drive:\n{ex.Message}",
                    "DRANOTCH - Google Drive Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        else
        {
            await SyncWithGoogleDriveAsync();
        }
    }

    public async Task SyncWithGoogleDriveAsync()
    {
        if (!_googleDriveSyncService.IsConnected) return;

        var deletedIds = _todoService.GetDeletedIds();
        var merged = await _googleDriveSyncService.SyncAsync(Todos.Select(t => t.ToModel()), deletedIds);
        if (merged != null)
        {
            OnTodosSyncedFromCloud(merged);
        }
    }

    private void OnTodosSyncedFromCloud(List<TodoItem> syncedList)
    {
        Action apply = () =>
        {
            var currentIds = Todos.ToDictionary(t => t.Id);

            foreach (var item in syncedList)
            {
                if (currentIds.TryGetValue(item.Id, out var existing))
                {
                    existing.Title = item.Title;
                    existing.IsCompleted = item.IsCompleted;
                    existing.DueDate = item.DueDate;
                }
                else
                {
                    var vm = new TodoItemViewModel(item);
                    vm.OnChanged += OnTodoItemChanged;
                    vm.OnDeleteRequested += DeleteTodo;
                    Todos.Add(vm);
                }
            }

            var syncedIds = syncedList.Select(s => s.Id).ToHashSet();
            var toRemove = Todos.Where(t => !syncedIds.Contains(t.Id)).ToList();
            foreach (var r in toRemove)
            {
                r.OnChanged -= OnTodoItemChanged;
                r.OnDeleteRequested -= DeleteTodo;
                Todos.Remove(r);
            }

            _todoService.SaveTodos(Todos.Select(t => t.ToModel()));
            OnPropertyChanged(nameof(PendingTodosCount));
            OnPropertyChanged(nameof(CompletedTodosCount));
            OnPropertyChanged(nameof(TotalTodosCount));
            OnPropertyChanged(nameof(HasCompletedTodos));
            OnPropertyChanged(nameof(HasPendingTodos));
            OnPropertyChanged(nameof(TodoSummaryText));
            _filteredTodos?.Refresh();
            UpdateTicker();
        };

        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(apply);
        }
        else
        {
            apply();
        }
    }

    public void NextTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count > 0)
        {
            _currentTickerIndex = (_currentTickerIndex + 1) % pending.Count;
            UpdateTicker();
        }
    }

    public void PreviousTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count > 0)
        {
            _currentTickerIndex = (_currentTickerIndex - 1 + pending.Count) % pending.Count;
            UpdateTicker();
        }
    }

    public void MoveTodo(TodoItemViewModel source, TodoItemViewModel target)
    {
        if (source == target) return;

        if (!string.Equals(source.CategoryGroup, target.CategoryGroup, StringComparison.OrdinalIgnoreCase))
        {
            source.Category = target.Category;
        }

        int oldIndex = Todos.IndexOf(source);
        int newIndex = Todos.IndexOf(target);

        if (oldIndex >= 0 && newIndex >= 0)
        {
            Todos.Move(oldIndex, newIndex);
            UpdateTodoCountsAndPersist();
        }
    }

    public void MoveCategoryUp(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;

        var order = GetCategoryOrder();
        int index = order.FindIndex(c => string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));
        if (index <= 0) return;

        string prevCategory = order[index - 1];
        order[index - 1] = order[index];
        order[index] = prevCategory;

        ReorderTodosByCategories(order);
    }

public void ExpandAllCategories()
    {
        TodoCategoryExpansionHelper.ExpandAll();
        _filteredTodos?.Refresh();
    }

    public void CollapseAllCategories()
    {
        var cats = AvailableCategories.Where(c => !string.Equals(c, "All", StringComparison.OrdinalIgnoreCase));
        TodoCategoryExpansionHelper.CollapseAll(cats);
        _filteredTodos?.Refresh();
    }

    public void ToggleCategoryCollapse(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;
        TodoCategoryExpansionHelper.Toggle(categoryName);
        _filteredTodos?.Refresh();
    }

    public void MoveCategoryDown(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;

        var order = GetCategoryOrder();
        int index = order.FindIndex(c => string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= order.Count - 1) return;

        string nextCategory = order[index + 1];
        order[index + 1] = order[index];
        order[index] = nextCategory;

        ReorderTodosByCategories(order);
    }

    public void MoveCategory(string sourceCategory, string targetCategory)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory) || string.IsNullOrWhiteSpace(targetCategory)) return;
        if (string.Equals(sourceCategory, targetCategory, StringComparison.OrdinalIgnoreCase)) return;

        var order = GetCategoryOrder();
        int sourceIndex = order.FindIndex(c => string.Equals(c, sourceCategory, StringComparison.OrdinalIgnoreCase));
        int targetIndex = order.FindIndex(c => string.Equals(c, targetCategory, StringComparison.OrdinalIgnoreCase));

        if (sourceIndex < 0 || targetIndex < 0) return;

        order.RemoveAt(sourceIndex);
        order.Insert(targetIndex, sourceCategory);

        ReorderTodosByCategories(order);
    }

    public List<string> GetCategoryOrder()
    {
        var order = new List<string>();
        foreach (var item in Todos)
        {
            string group = item.CategoryGroup;
            if (!order.Any(c => string.Equals(c, group, StringComparison.OrdinalIgnoreCase)))
            {
                order.Add(group);
            }
        }
        return order;
    }

    public void ReorderTodosByCategories(List<string> categoryOrder)
    {
        var reordered = new List<TodoItemViewModel>();

        foreach (var cat in categoryOrder)
        {
            var itemsInCat = Todos
                .Where(t => string.Equals(t.CategoryGroup, cat, StringComparison.OrdinalIgnoreCase))
                .ToList();
            reordered.AddRange(itemsInCat);
        }

        foreach (var item in Todos)
        {
            if (!reordered.Contains(item))
            {
                reordered.Add(item);
            }
        }

        for (int targetIdx = 0; targetIdx < reordered.Count; targetIdx++)
        {
            var targetItem = reordered[targetIdx];
            int currentIdx = Todos.IndexOf(targetItem);
            if (currentIdx >= 0 && currentIdx != targetIdx)
            {
                Todos.Move(currentIdx, targetIdx);
            }
        }

        UpdateTodoCountsAndPersist();
    }

    private void CompleteCurrentTicker()
    {
        var firstPending = Todos.FirstOrDefault(t => !t.IsCompleted);
        if (firstPending != null)
        {
            firstPending.IsCompleted = true;
        }
    }

    public void UpdateTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count == 0)
        {
            _currentTickerItem = null;
            _currentTickerIndex = 0;
            if (Todos.Count > 0)
            {
                _currentTickerText = "Semua tugas selesai 🎉";
                _currentTickerBadge = "✓";
            }
            else
            {
                _currentTickerText = "Belum ada tugas";
                _currentTickerBadge = "+";
            }
        }
        else
        {
            if (_currentTickerIndex >= pending.Count || _currentTickerIndex < 0)
            {
                _currentTickerIndex = 0;
            }

            _currentTickerItem = pending[_currentTickerIndex];
            _currentTickerBadge = $"{pending.Count}";

            // Aliran teks berjalan bersambung dari kanan ke kiri lintas kategori
            var parts = pending.Select(t => t.HasCategory ? $"[{t.Category}] {t.TaskText}" : t.Title);
            _currentTickerText = string.Join("     •     ", parts);
        }

        OnPropertyChanged(nameof(CurrentTickerText));
        OnPropertyChanged(nameof(CurrentTickerBadge));
        OnPropertyChanged(nameof(CurrentTickerItem));
        OnPropertyChanged(nameof(HasPendingTodos));
        OnPropertyChanged(nameof(ShowFullTodoInCompact));
    }

    private DispatcherTimer? _expandHoverTimer;

    public void ToggleMiniMode()
    {
        _idleTimer?.Stop();
        if (!IsIdleMiniMode)
        {
            if (IsPinned)
            {
                IsPinned = false;
            }
            IsIdleMiniMode = true;
        }
        else
        {
            IsIdleMiniMode = false;
            if (!IsPinned && !_isHovering)
            {
                ResetIdleTimer();
            }
        }
    }

    public void EnterIdleMiniMode()
    {
        if (IsPinned || IsLyricsMode || IsComponentsMenuOpen || HasActivePrayerNotification)
        {
            return;
        }

        IsIdleMiniMode = true;
    }

    public void WakeFromIdle()
    {
        _idleTimer?.Stop();
        if (_isIdleMiniMode)
        {
            IsIdleMiniMode = false;
        }

        if (!IsPinned && !_isHovering)
        {
            ResetIdleTimer();
        }
    }

    public void ResetIdleTimer()
    {
        _idleTimer?.Stop();
        if (IsPinned || IsLyricsMode)
        {
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher == null)
        {
            return;
        }

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _idleTimer.Tick += (s, e) =>
        {
            _idleTimer?.Stop();
            if (!_isHoveringWindow && !IsPinned && !IsLyricsMode && !IsComponentsMenuOpen && !HasActivePrayerNotification)
            {
                IsHovering = false;
                EnterIdleMiniMode();
            }
        };
        _idleTimer.Start();
    }

    public void OnMouseEnter()
    {
        _isHoveringWindow = true;
        IsHovering = true;
        _expandHoverTimer?.Stop();
        _idleTimer?.Stop();
        OnPropertyChanged(nameof(IsNotchActive));

        // Check for any cloud changes immediately when approaching the notch
        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
        {
            _ = _googleDriveSyncService.CheckForRemoteChangesAsync();
        }

        // Fitur On Hover dinonaktifkan: panel tidak lagi terbuka otomatis saat kursor diarahkan
        if (false && Settings.ExpandOnHover && CurrentMode == PanelMode.Compact && !IsPinned)
        {
            _expandHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _expandHoverTimer.Tick += (s, e) =>
            {
                _expandHoverTimer?.Stop();
                if (_isHovering && CurrentMode == PanelMode.Compact && !IsPinned)
                {
                    RequestExpand?.Invoke();
                }
            };
            _expandHoverTimer.Start();
        }
    }

    public void OnMouseLeave()
    {
        _isHoveringWindow = false;
        _expandHoverTimer?.Stop();
        OnPropertyChanged(nameof(IsNotchActive));

        if (!IsPinned)
        {
            ResetIdleTimer();
        }

        // Fitur On Hover dinonaktifkan: panel tidak lagi tertutup otomatis saat kursor keluar
        if (false && Settings.ExpandOnHover && CurrentMode == PanelMode.Expanded && !IsPinned)
        {
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                if (!_isHovering && !IsPinned && CurrentMode == PanelMode.Expanded)
                {
                    RequestCollapse?.Invoke();
                }
            };
            delayTimer.Start();
        }
    }
}
