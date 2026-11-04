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
using NotchMusic.Services.Bmkg;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.LiveTv;
using NotchMusic.Services.Media;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Radio;
using NotchMusic.Services.Reminder;
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
    private readonly ILiveTvService _liveTvService;
    private readonly IBmkgService _bmkgService;
    private readonly IReminderService _reminderService;
    private readonly DispatcherTimer _interpolationTimer;

    private PrayerNotificationEvent? _activePrayerNotification;
    private DispatcherTimer? _notificationDismissTimer;
    private bool _isPrayerPanelOpen;

    // Pengingat Pintar Fields (Komponen ke-8)
    private ReminderNotificationEvent? _activeReminderNotification;
    private DispatcherTimer? _reminderNotificationDismissTimer;
    private bool _isAddingOrEditingReminder;
    private string? _editingReminderId;
    private string _formReminderTitle = string.Empty;
    private string _formReminderCategory = "Hidrasi";
    private string _formReminderIcon = "💧";
    private string _formReminderNotes = string.Empty;
    private ReminderType _formReminderType = ReminderType.Interval;
    private int _formReminderIntervalMinutes = 60;
    private string _formReminderDailyTimesText = "08:00, 13:00, 20:00";

    // BMKG Cuaca & Gempa Bumi Fields (Komponen ke-7)
    private BmkgNotificationEvent? _activeBmkgNotification;
    private DispatcherTimer? _bmkgNotificationDismissTimer;
    private string _bmkgActiveTab = "Weather"; // "Weather", "Earthquake"
    private string _bmkgEarthquakeSubTab = "AutoGempa"; // "AutoGempa", "Terkini", "Dirasakan"
    private bool _isShowingShakemap = true;
    private bool _isEditingCustomAdm4;
    private string _bmkgLocationStatus = string.Empty;
    private bool _isSelectingPreciseRegion;
    private bool _isSyncingRegionSelectors;
    private BmkgRegionItem? _selectedBmkgProvince;
    private BmkgRegionItem? _selectedBmkgRegency;
    private BmkgRegionItem? _selectedBmkgDistrict;
    private BmkgRegionItem? _selectedBmkgVillage;
    private string _bmkgLocationSearchQuery = string.Empty;
    private bool _isSearchingBmkgLocation;
    private CancellationTokenSource? _bmkgLocationSearchCts;

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

    // Live TV Fields (Komponen ke-6)
    private readonly ICollectionView _filteredLiveTvChannels;
    private string _selectedLiveTvCountryCode = "ALL";
    private string _liveTvCategoryFilter = "All";
    private string _liveTvSearchQuery = string.Empty;
    private bool _isSearchingOnlineLiveTv;
    private bool _isAddingCustomLiveTv;
    private string _customLiveTvName = string.Empty;
    private string _customLiveTvUrl = string.Empty;
    private CancellationTokenSource? _liveTvSearchCts;
    private bool _hasLoadedInitialLiveTvCatalog;

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
    public event Action? RequestOpenSpotify;
    public event Action? PlaybackStartedOnSpotify;
    public event Action? PlaybackStartedOnYouTubeMusic;
    public event Action? RequestOpenFloatingBrowser;
    public event Action? RequestOpenRadio;
    public event Action? RequestOpenLiveTv;
    public event Action? RequestOpenBmkg;
    public event Action? RequestOpenReminder;
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
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowHoverReminderPill));
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
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowHoverReminderPill));
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
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowHoverReminderPill));
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
    public bool ShowMusicInNotch => ShowComponentIcons;
    public bool ShowMusicPillInNotch => ShowMusicInNotch && !IsMusicWindowOpen;
    public bool ShowMusicIconInNotch => ShowMusicInNotch && IsMusicWindowOpen;
    public bool ShowIdlePrayerBesideLogo => false;
    public bool ShowIdleWeatherBesideLogo => false;
    public bool ShowHoverWeatherPill => IsCompactMode && !_isBezelMinimized && ShowComponentIcons && Settings.EnableBmkg;

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

    // ================= LIVE TV PROPERTIES =================
    public ILiveTvService LiveTvService => _liveTvService;
    public ObservableCollection<LiveTvChannel> LiveTvChannels { get; } = new();
    public ObservableCollection<LiveTvCountryOption> LiveTvCountries { get; } = new();
    public ICollectionView FilteredLiveTvChannels => _filteredLiveTvChannels;

    public bool IsLiveTvWindowOpen
    {
        get => Settings.IsLiveTvWindowOpen;
        set
        {
            if (Settings.IsLiveTvWindowOpen != value)
            {
                Settings.IsLiveTvWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsLiveTvWindowOpen));
                OnPropertyChanged(nameof(IsLiveTvActiveOrOpen));
                OnPropertyChanged(nameof(LiveTvNotchTooltip));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsLiveTvWindowCompact
    {
        get => Settings.IsLiveTvWindowCompact;
        set
        {
            if (Settings.IsLiveTvWindowCompact != value)
            {
                Settings.IsLiveTvWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsLiveTvWindowCompact));
                OnPropertyChanged(nameof(IsLiveTvWindowExpanded));
            }
        }
    }

    public bool IsLiveTvWindowExpanded => !IsLiveTvWindowCompact;

    public LiveTvChannel? CurrentLiveTvChannel => _liveTvService.CurrentChannel;
    public string CurrentLiveTvChannelName => _liveTvService.CurrentChannel?.Name ?? "Pilih Saluran TV";
    public string CurrentLiveTvChannelSubtitle => _liveTvService.CurrentChannel?.Subtitle ?? "Live TV Indonesia & Dunia";
    public string CurrentLiveTvFavoriteSymbol => _liveTvService.CurrentChannel?.FavoriteSymbol ?? "☆";
    public string CurrentLiveTvSourceBadge => _liveTvService.CurrentChannel?.SourceBadgeText ?? "HLS";

    public bool IsLiveTvPlaying => _liveTvService.PlaybackState == LiveTvPlaybackState.Playing;
    public bool IsLiveTvBuffering => _liveTvService.PlaybackState == LiveTvPlaybackState.Buffering;
    public bool IsLiveTvActiveOrOpen => IsLiveTvPlaying || IsLiveTvBuffering || IsLiveTvWindowOpen;

    public string LiveTvStateBadgeText => _liveTvService.PlaybackState switch
    {
        LiveTvPlaybackState.Playing => "LIVE",
        LiveTvPlaybackState.Buffering => "BUFFERING",
        LiveTvPlaybackState.Paused => "PAUSED",
        LiveTvPlaybackState.Error => "OFFLINE",
        _ => "SIAP"
    };

    public string LiveTvStatusMessage => _liveTvService.StatusMessage;

    public string LiveTvNotchTooltip => _liveTvService.CurrentChannel != null
        ? $"Live TV: {_liveTvService.CurrentChannel.Name} ({LiveTvStateBadgeText})\nKlik untuk membuka pemutar & katalog saluran Live TV"
        : "Buka Live TV (Streaming TV Indonesia & Dunia)";

    public double LiveTvVolume
    {
        get => _liveTvService.Volume;
        set
        {
            _liveTvService.Volume = value;
            if (value > 0 && _liveTvService.IsMuted)
            {
                _liveTvService.IsMuted = false;
            }
            OnPropertyChanged(nameof(LiveTvVolume));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
        }
    }

    public bool IsLiveTvMuted
    {
        get => _liveTvService.IsMuted;
        set
        {
            _liveTvService.IsMuted = value;
            OnPropertyChanged(nameof(IsLiveTvMuted));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
        }
    }

    public string LiveTvVolumePercentText => IsLiveTvMuted ? "Mute" : $"{Math.Round(LiveTvVolume * 100)}%";
    public string LiveTvVolumeIcon => IsLiveTvMuted || LiveTvVolume <= 0.01 ? "🔇" : (LiveTvVolume < 0.5 ? "🔉" : "🔊");

    public string SelectedLiveTvCountryCode
    {
        get => _selectedLiveTvCountryCode;
        set
        {
            if (SetProperty(ref _selectedLiveTvCountryCode, value ?? "ALL"))
            {
                Settings.LastLiveTvCountryCode = _selectedLiveTvCountryCode;
                _settingsService.Save();
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
                _ = LoadSelectedLiveTvCountryAsync(_selectedLiveTvCountryCode);
            }
        }
    }

    public string LiveTvCategoryFilter
    {
        get => _liveTvCategoryFilter;
        set
        {
            if (SetProperty(ref _liveTvCategoryFilter, value ?? "All"))
            {
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
                _ = LoadSelectedLiveTvCategoryIfNeededAsync(_liveTvCategoryFilter);
            }
        }
    }

    public string LiveTvSearchQuery
    {
        get => _liveTvSearchQuery;
        set
        {
            if (SetProperty(ref _liveTvSearchQuery, value ?? string.Empty))
            {
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        }
    }

    public string LiveTvCatalogCountText
    {
        get
        {
            var count = _filteredLiveTvChannels?.Cast<object>().Count() ?? LiveTvChannels.Count;
            return $"{count} Saluran";
        }
    }

    public bool IsSearchingOnlineLiveTv
    {
        get => _isSearchingOnlineLiveTv;
        set
        {
            if (SetProperty(ref _isSearchingOnlineLiveTv, value))
            {
                OnPropertyChanged(nameof(SearchOnlineLiveTvButtonText));
            }
        }
    }

    public string SearchOnlineLiveTvButtonText => IsSearchingOnlineLiveTv ? "Mencari..." : "🌐 Cari Online";

    public bool IsAddingCustomLiveTv
    {
        get => _isAddingCustomLiveTv;
        set => SetProperty(ref _isAddingCustomLiveTv, value);
    }

    public string CustomLiveTvName
    {
        get => _customLiveTvName;
        set => SetProperty(ref _customLiveTvName, value);
    }

    public string CustomLiveTvUrl
    {
        get => _customLiveTvUrl;
        set => SetProperty(ref _customLiveTvUrl, value);
    }

    // ================= BMKG CUACA & GEMPA BUMI PROPERTIES (KOMPONEN KE-7) =================
    public IBmkgService BmkgService => _bmkgService;
    public List<CityLocation> BmkgCities { get; } = CityLocation.GetIndonesianCities();

    public bool EnableBmkg
    {
        get => Settings.EnableBmkg;
        set
        {
            if (Settings.EnableBmkg != value)
            {
                Settings.EnableBmkg = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(EnableBmkg));
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(HasActiveBmkgNotification));
                if (value)
                {
                    _bmkgService.Start();
                }
                else
                {
                    _bmkgService.Stop();
                    ActiveBmkgNotification = null;
                }
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool ShowWeatherInCompactNotch
    {
        get => Settings.ShowWeatherInCompactNotch;
        set
        {
            if (Settings.ShowWeatherInCompactNotch != value)
            {
                Settings.ShowWeatherInCompactNotch = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(ShowWeatherInCompactNotch));
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
            }
        }
    }

    public bool IsBmkgWindowOpen
    {
        get => Settings.IsBmkgWindowOpen;
        set
        {
            if (Settings.IsBmkgWindowOpen != value)
            {
                Settings.IsBmkgWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBmkgWindowOpen));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsBmkgWindowCompact
    {
        get => Settings.IsBmkgWindowCompact;
        set
        {
            if (Settings.IsBmkgWindowCompact != value)
            {
                Settings.IsBmkgWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBmkgWindowCompact));
                OnPropertyChanged(nameof(IsBmkgWindowExpanded));
            }
        }
    }

    public bool IsBmkgWindowExpanded => !IsBmkgWindowCompact;

    public string BmkgActiveTab
    {
        get => _bmkgActiveTab;
        set
        {
            if (SetProperty(ref _bmkgActiveTab, value ?? "Weather"))
            {
                OnPropertyChanged(nameof(IsBmkgWeatherTab));
                OnPropertyChanged(nameof(IsBmkgEarthquakeTab));
            }
        }
    }

    public bool IsBmkgWeatherTab => string.Equals(_bmkgActiveTab, "Weather", StringComparison.OrdinalIgnoreCase);
    public bool IsBmkgEarthquakeTab => string.Equals(_bmkgActiveTab, "Earthquake", StringComparison.OrdinalIgnoreCase);

    public string BmkgEarthquakeSubTab
    {
        get => _bmkgEarthquakeSubTab;
        set
        {
            if (SetProperty(ref _bmkgEarthquakeSubTab, value ?? "AutoGempa"))
            {
                OnPropertyChanged(nameof(IsBmkgEqAutoTab));
                OnPropertyChanged(nameof(IsBmkgEqTerkiniTab));
                OnPropertyChanged(nameof(IsBmkgEqDirasakanTab));
            }
        }
    }

    public bool IsBmkgEqAutoTab => string.Equals(_bmkgEarthquakeSubTab, "AutoGempa", StringComparison.OrdinalIgnoreCase);
    public bool IsBmkgEqTerkiniTab => string.Equals(_bmkgEarthquakeSubTab, "Terkini", StringComparison.OrdinalIgnoreCase);
    public bool IsBmkgEqDirasakanTab => string.Equals(_bmkgEarthquakeSubTab, "Dirasakan", StringComparison.OrdinalIgnoreCase);

    public bool IsShowingShakemap
    {
        get => _isShowingShakemap;
        set => SetProperty(ref _isShowingShakemap, value);
    }

    public bool IsEditingCustomAdm4
    {
        get => _isEditingCustomAdm4;
        set => SetProperty(ref _isEditingCustomAdm4, value);
    }

    public bool IsSelectingPreciseRegion
    {
        get => _isSelectingPreciseRegion;
        set
        {
            if (SetProperty(ref _isSelectingPreciseRegion, value) && value)
            {
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
            }
        }
    }

    public IReadOnlyList<BmkgRegionItem> BmkgProvinces => _bmkgService.GetProvinces();
    public ObservableCollection<BmkgRegionItem> BmkgRegencies { get; } = new();
    public ObservableCollection<BmkgRegionItem> BmkgDistricts { get; } = new();
    public ObservableCollection<BmkgRegionItem> BmkgVillages { get; } = new();
    public ObservableCollection<BmkgLocationSearchResult> BmkgLocationSearchResults { get; } = new();
    public bool HasBmkgLocationSearchResults => BmkgLocationSearchResults.Count > 0;

    public string BmkgLocationSearchQuery
    {
        get => _bmkgLocationSearchQuery;
        set => SetProperty(ref _bmkgLocationSearchQuery, value ?? string.Empty);
    }

    public bool IsSearchingBmkgLocation
    {
        get => _isSearchingBmkgLocation;
        set
        {
            if (SetProperty(ref _isSearchingBmkgLocation, value))
            {
                OnPropertyChanged(nameof(BmkgLocationSearchButtonText));
            }
        }
    }

    public string BmkgLocationSearchButtonText => IsSearchingBmkgLocation ? "Mencari..." : "Cari";

    public BmkgRegionItem? SelectedBmkgProvince
    {
        get => _selectedBmkgProvince;
        set
        {
            if (SetProperty(ref _selectedBmkgProvince, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedProvinceChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgRegency
    {
        get => _selectedBmkgRegency;
        set
        {
            if (SetProperty(ref _selectedBmkgRegency, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedRegencyChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgDistrict
    {
        get => _selectedBmkgDistrict;
        set
        {
            if (SetProperty(ref _selectedBmkgDistrict, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedDistrictChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgVillage
    {
        get => _selectedBmkgVillage;
        set
        {
            if (SetProperty(ref _selectedBmkgVillage, value) && !_isSyncingRegionSelectors && value != null)
            {
                ApplyPreciseVillageSelection(value);
            }
        }
    }

    public string BmkgLocationStatus
    {
        get => _bmkgLocationStatus;
        set => SetProperty(ref _bmkgLocationStatus, value);
    }

    public bool BmkgFollowPrayerCity
    {
        get => Settings.BmkgFollowPrayerCity;
        set
        {
            if (Settings.BmkgFollowPrayerCity != value)
            {
                Settings.BmkgFollowPrayerCity = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(BmkgFollowPrayerCity));
                OnPropertyChanged(nameof(SelectedBmkgCity));
                OnPropertyChanged(nameof(BmkgWeatherCityText));
                _ = _bmkgService.RefreshWeatherAsync();
            }
        }
    }

    public CityLocation? SelectedBmkgCity
    {
        get
        {
            string target = Settings.BmkgFollowPrayerCity ? Settings.PrayerCity : Settings.BmkgSelectedCity;
            return BmkgCities.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase))
                   ?? CityLocation.FindBestMatchOrNearest(target, Settings.PrayerLatitude, Settings.PrayerLongitude);
        }
        set
        {
            if (value != null)
            {
                Settings.BmkgSelectedCity = value.Name;
                Settings.BmkgCustomAdm4Code = value.BmkgAdm4Code;
                if (Settings.BmkgFollowPrayerCity)
                {
                    Settings.PrayerCity = value.Name;
                    Settings.PrayerLatitude = value.Latitude;
                    Settings.PrayerLongitude = value.Longitude;
                    Settings.PrayerTimezone = value.Timezone;
                    _prayerReminderService.RefreshSchedule();
                    OnPropertyChanged(nameof(CurrentPrayerSchedule));
                    OnPropertyChanged(nameof(NextPrayerName));
                    OnPropertyChanged(nameof(NextPrayerTimeFormatted));
                    OnPropertyChanged(nameof(NextPrayerCountdownBadge));
                }
                _settingsService.Save();
                OnPropertyChanged(nameof(SelectedBmkgCity));
                OnPropertyChanged(nameof(BmkgCustomAdm4Code));
                OnPropertyChanged(nameof(BmkgActiveAdm4Text));
                OnPropertyChanged(nameof(BmkgWeatherCityText));
                BmkgLocationStatus = $"✓ Kota dipilih: {value.DisplayName} (Anda dapat memilih Kecamatan/Kelurahan lebih spesifik di bawah)";
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
                _ = _bmkgService.RefreshWeatherAsync();
            }
        }
    }

    public string BmkgCustomAdm4Code
    {
        get => Settings.BmkgCustomAdm4Code;
        set
        {
            if (Settings.BmkgCustomAdm4Code != value)
            {
                Settings.BmkgCustomAdm4Code = value?.Trim() ?? string.Empty;
                _settingsService.Save();
                OnPropertyChanged(nameof(BmkgCustomAdm4Code));
                OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            }
        }
    }

    public string BmkgActiveAdm4Text => $"Kode ADM4: {_bmkgService.GetActiveAdm4Code()}";

    public BmkgWeatherData? CurrentBmkgWeather => _bmkgService.CurrentWeatherData;
    public BmkgWeatherForecastItem? CurrentWeatherSlot => _bmkgService.CurrentWeatherData?.CurrentWeather;
    public List<BmkgWeatherDayGroup> BmkgWeatherDayGroups => _bmkgService.CurrentWeatherData?.DayGroups ?? new();

    public string BmkgWeatherEmoji => CurrentWeatherSlot?.WeatherEmoji ?? "🌤";
    public string BmkgWeatherIconGeometry => CurrentWeatherSlot?.WeatherIconGeometry ?? BmkgWeatherForecastItem.PartlyCloudyGeometry;
    public string BmkgWeatherIconColor => CurrentWeatherSlot?.WeatherIconColor ?? "#FFD43B";
    public string BmkgWeatherTempText => CurrentWeatherSlot != null ? $"{CurrentWeatherSlot.TemperatureC}°C" : "--°C";
    public string BmkgWeatherDescText => CurrentWeatherSlot?.WeatherDesc ?? "Memuat cuaca...";
    public string BmkgWeatherCityText => _bmkgService.CurrentWeatherData?.CompactCityLabel ?? _bmkgService.GetActiveCityName();
    public string BmkgWeatherFullCityText => _bmkgService.CurrentWeatherData?.ShortCityLabel ?? _bmkgService.GetActiveCityName();
    public string BmkgWeatherLocationSubtitle => _bmkgService.CurrentWeatherData?.FullLocationSubtitle ?? "Indonesia • BMKG";
    public string BmkgWeatherRangeText => _bmkgService.CurrentWeatherData?.TodayTempRangeFormatted ?? "-";
    public string BmkgWeatherHumidityText => CurrentWeatherSlot?.HumidityFormatted ?? "--%";
    public string BmkgWeatherWindText => CurrentWeatherSlot != null ? $"{CurrentWeatherSlot.WindSpeedFormatted} ({CurrentWeatherSlot.WindDirectionLabel})" : "-";
    public string BmkgWeatherCloudText => CurrentWeatherSlot?.CloudCoverFormatted ?? "--%";
    public string BmkgWeatherRainText => CurrentWeatherSlot?.PrecipitationFormatted ?? "0 mm";

    public BmkgEarthquakeItem? LatestEarthquake => _bmkgService.LatestEarthquake;
    public IReadOnlyList<BmkgEarthquakeItem> RecentEarthquakes => _bmkgService.RecentEarthquakes;
    public IReadOnlyList<BmkgEarthquakeItem> FeltEarthquakes => _bmkgService.FeltEarthquakes;
    public bool HasLatestEarthquake => _bmkgService.LatestEarthquake != null;

    public string LatestEarthquakeBadge => LatestEarthquake != null ? $"M {LatestEarthquake.Magnitude}" : "Gempa";
    public string LatestEarthquakeCompactText => LatestEarthquake != null
        ? $"M {LatestEarthquake.Magnitude} • {LatestEarthquake.Wilayah}"
        : "Belum ada data gempa terkini";

    public string BmkgStatusMessage => _bmkgService.StatusMessage;
    public bool IsBmkgLoading => _bmkgService.IsLoadingWeather || _bmkgService.IsLoadingEarthquake;

    public string BmkgNotchTooltip
    {
        get
        {
            var weatherLine = CurrentWeatherSlot != null
                ? $"🌤️ Cuaca BMKG ({BmkgWeatherCityText}): {BmkgWeatherTempText} • {BmkgWeatherDescText} (Kelembapan {BmkgWeatherHumidityText})"
                : $"🌤️ Cuaca BMKG ({BmkgWeatherCityText}): Memuat...";

            var eqLine = LatestEarthquake != null
                ? $"🌋 Gempa Terbaru: M {LatestEarthquake.Magnitude} • {LatestEarthquake.Wilayah} ({LatestEarthquake.Jam}, Kedalaman {LatestEarthquake.Kedalaman})"
                : "🌋 Gempa Bumi BMKG: Siap memantau...";

            return $"{weatherLine}\n{eqLine}\nKlik untuk membuka jendela Cuaca & Gempa Bumi BMKG";
        }
    }

    public bool HasActiveBmkgNotification => EnableBmkg && _activeBmkgNotification != null;
    public BmkgNotificationEvent? ActiveBmkgNotification
    {
        get => _activeBmkgNotification;
        private set
        {
            if (SetProperty(ref _activeBmkgNotification, value))
            {
                OnPropertyChanged(nameof(HasActiveBmkgNotification));
            }
        }
    }

    // ================= PENGINGAT PINTAR (MINUM AIR, OBAT, DLL.) PROPERTIES (KOMPONEN KE-8) =================
    public IReminderService ReminderService => _reminderService;
    public ObservableCollection<ReminderItem> Reminders { get; } = new();
    public ObservableCollection<ReminderItem> ActiveReminders { get; } = new();
    public IReadOnlyList<ReminderPresetOption> ReminderPresets => _reminderService.Presets;
    public IReadOnlyList<string> AvailableReminderIcons { get; } = new[] { "💧", "💊", "👀", "🧘", "🍽️", "☕", "⏰", "🏃" };
    public IReadOnlyList<ReminderIconOption> AvailableReminderIconOptions { get; } = new[]
    {
        new ReminderIconOption { Key = "💧", Label = "Air" },
        new ReminderIconOption { Key = "💊", Label = "Obat" },
        new ReminderIconOption { Key = "👀", Label = "Mata" },
        new ReminderIconOption { Key = "🧘", Label = "Regang" },
        new ReminderIconOption { Key = "🍽️", Label = "Makan" },
        new ReminderIconOption { Key = "☕", Label = "Kopi/Teh" },
        new ReminderIconOption { Key = "🏃", Label = "Olahraga" },
        new ReminderIconOption { Key = "⏰", Label = "Alarm" }
    };

    public bool EnableReminders
    {
        get => Settings.EnableReminders;
        set
        {
            if (Settings.EnableReminders != value)
            {
                Settings.EnableReminders = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(EnableReminders));
                OnPropertyChanged(nameof(ShowHoverReminderPill));
                OnPropertyChanged(nameof(HasActiveReminderNotification));
                if (value)
                {
                    _reminderService.Start();
                }
                else
                {
                    _reminderService.Stop();
                    ActiveReminderNotification = null;
                }
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool ShowReminderInCompactNotch
    {
        get => Settings.ShowReminderInCompactNotch;
        set
        {
            if (Settings.ShowReminderInCompactNotch != value)
            {
                Settings.ShowReminderInCompactNotch = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(ShowReminderInCompactNotch));
                OnPropertyChanged(nameof(ShowHoverReminderPill));
            }
        }
    }

    public bool IsReminderWindowOpen
    {
        get => Settings.IsReminderWindowOpen;
        set
        {
            if (Settings.IsReminderWindowOpen != value)
            {
                Settings.IsReminderWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsReminderWindowOpen));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsReminderWindowCompact
    {
        get => Settings.IsReminderWindowCompact;
        set
        {
            if (Settings.IsReminderWindowCompact != value)
            {
                Settings.IsReminderWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsReminderWindowCompact));
                OnPropertyChanged(nameof(IsReminderWindowExpanded));
            }
        }
    }

    public bool IsReminderWindowExpanded => !IsReminderWindowCompact;

    public bool ShowHoverReminderPill => IsCompactMode && !_isBezelMinimized && ShowComponentIcons && Settings.EnableReminders && Settings.ShowReminderInCompactNotch && HasActiveReminders;

    public ReminderItem? NextUpcomingReminder => _reminderService.NextUpcomingReminder;
    public bool HasActiveReminders => Reminders.Any(r => r.IsEnabled);
    public int ActiveRemindersCount => Reminders.Count(r => r.IsEnabled);
    public int CompletedTodayTotalCount => Reminders.Where(r => r.LastCompletedDate.Date == DateTime.Today).Sum(r => r.CompletedTodayCount);
    public string ReminderSummaryText => ActiveRemindersCount > 0
        ? $"{ActiveRemindersCount} pengingat aktif • {CompletedTodayTotalCount}x selesai hari ini"
        : "Belum ada pengingat aktif";

    public string NextReminderIcon => NextUpcomingReminder?.Icon ?? "💧";
    public string NextReminderIconGeometry => ReminderItem.GetIconGeometryForKey(NextReminderIcon);
    public string NextReminderAccentColor => ReminderItem.GetAccentColorForKey(NextReminderIcon);
    public string NextReminderBadgeBackground => ReminderItem.GetBadgeBackgroundForKey(NextReminderIcon);
    public string NextReminderBadgeBorder => ReminderItem.GetBadgeBorderForKey(NextReminderIcon);
    public string NextReminderTitle => NextUpcomingReminder?.Title ?? "Pengingat Pintar";
    public string NextReminderShortTitle
    {
        get
        {
            var t = NextUpcomingReminder?.Title;
            if (string.IsNullOrWhiteSpace(t)) return "Pengingat";
            return t.Length > 18 ? t.Substring(0, 17).Trim() + "…" : t;
        }
    }
    public string NextReminderTimeFormatted => NextUpcomingReminder?.NextTriggerFormatted ?? "-";
    public string NextReminderCountdownBadge => NextUpcomingReminder?.CountdownText ?? "Siap";
    public string NextReminderScheduleText => NextUpcomingReminder?.ScheduleSummaryText ?? "Klik untuk atur pengingat minum air, obat, dll.";

    public string ReminderNotchTooltip
    {
        get
        {
            var active = Reminders.Where(r => r.IsEnabled).ToList();
            if (active.Count > 0)
            {
                var lines = active.Select(r => $"{r.Icon} {r.Title}: {r.CountdownText} ({r.NextTriggerFormatted})");
                return "Pengingat Aktif:\n" + string.Join("\n", lines) + "\nKlik untuk membuka jendela Pengingat Pintar";
            }
            return "💧💊 Pengingat Pintar (Minum Air, Minum Obat, Istirahat Mata, dll.)\nKlik untuk membuka dan mengatur jadwal pengingat";
        }
    }

    public bool IsAddingOrEditingReminder
    {
        get => _isAddingOrEditingReminder;
        set
        {
            if (SetProperty(ref _isAddingOrEditingReminder, value))
            {
                OnPropertyChanged(nameof(ReminderFormHeaderTitle));
                OnPropertyChanged(nameof(ReminderFormSaveButtonText));
            }
        }
    }

    public bool IsEditingExistingReminder => !string.IsNullOrEmpty(_editingReminderId);
    public string ReminderFormHeaderTitle => IsEditingExistingReminder ? "Edit Pengingat" : "Tambah Pengingat Baru";
    public string ReminderFormSaveButtonText => IsEditingExistingReminder ? "Simpan Perubahan" : "Tambah Pengingat";

    public string FormReminderTitle
    {
        get => _formReminderTitle;
        set => SetProperty(ref _formReminderTitle, value ?? string.Empty);
    }

    public string FormReminderCategory
    {
        get => _formReminderCategory;
        set => SetProperty(ref _formReminderCategory, value ?? "Kesehatan");
    }

    public string FormReminderIcon
    {
        get => _formReminderIcon;
        set
        {
            if (SetProperty(ref _formReminderIcon, string.IsNullOrWhiteSpace(value) ? "💧" : value))
            {
                OnPropertyChanged(nameof(FormReminderIconGeometry));
                OnPropertyChanged(nameof(FormReminderAccentColor));
                OnPropertyChanged(nameof(FormReminderBadgeBackground));
                OnPropertyChanged(nameof(FormReminderBadgeBorder));
            }
        }
    }

    public string FormReminderIconGeometry => ReminderItem.GetIconGeometryForKey(_formReminderIcon);
    public string FormReminderAccentColor => ReminderItem.GetAccentColorForKey(_formReminderIcon);
    public string FormReminderBadgeBackground => ReminderItem.GetBadgeBackgroundForKey(_formReminderIcon);
    public string FormReminderBadgeBorder => ReminderItem.GetBadgeBorderForKey(_formReminderIcon);

    public string FormReminderNotes
    {
        get => _formReminderNotes;
        set => SetProperty(ref _formReminderNotes, value ?? string.Empty);
    }

    public ReminderType FormReminderType
    {
        get => _formReminderType;
        set
        {
            if (SetProperty(ref _formReminderType, value))
            {
                OnPropertyChanged(nameof(IsFormIntervalType));
                OnPropertyChanged(nameof(IsFormDailyTimeType));
            }
        }
    }

    public bool IsFormIntervalType => _formReminderType == ReminderType.Interval;
    public bool IsFormDailyTimeType => _formReminderType == ReminderType.DailyTime;

    public int FormReminderIntervalMinutes
    {
        get => _formReminderIntervalMinutes;
        set => SetProperty(ref _formReminderIntervalMinutes, Math.Clamp(value, 1, 1440));
    }

    public string FormReminderDailyTimesText
    {
        get => _formReminderDailyTimesText;
        set => SetProperty(ref _formReminderDailyTimesText, value ?? "08:00");
    }

    public bool HasActiveReminderNotification => EnableReminders && _activeReminderNotification != null;
    public ReminderNotificationEvent? ActiveReminderNotification
    {
        get => _activeReminderNotification;
        private set
        {
            if (SetProperty(ref _activeReminderNotification, value))
            {
                OnPropertyChanged(nameof(HasActiveReminderNotification));
                OnPropertyChanged(nameof(IsNotchActive));
            }
        }
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
    public bool IsAudioActive => IsPlaying || IsRadioPlaying || IsLiveTvPlaying;
    public bool IsNotchActive => IsAudioActive || IsRadioBuffering || IsRadioWindowOpen || IsLiveTvBuffering || IsLiveTvWindowOpen || IsBrowserWindowOpen || CurrentMode != PanelMode.Compact || IsComponentsMenuOpen || HasActivePrayerNotification || _isHovering;

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
    public ICommand OpenSpotifyCommand { get; }
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
    public ICommand OpenLiveTvCommand { get; }
    public ICommand ToggleLiveTvWindowCommand { get; }
    public ICommand CloseLiveTvWindowCommand { get; }
    public ICommand MinimizeLiveTvWindowCommand { get; }
    public ICommand ExpandLiveTvWindowCommand { get; }
    public ICommand ToggleLiveTvPlayPauseCommand { get; }
    public ICommand StopLiveTvCommand { get; }
    public ICommand NextLiveTvChannelCommand { get; }
    public ICommand PreviousLiveTvChannelCommand { get; }
    public ICommand PlayLiveTvChannelCommand { get; }
    public ICommand CycleLiveTvSourceCommand { get; }
    public ICommand SetLiveTvCategoryCommand { get; }
    public ICommand ToggleCurrentLiveTvFavoriteCommand { get; }
    public ICommand ToggleLiveTvChannelFavoriteCommand { get; }
    public ICommand ToggleLiveTvMuteCommand { get; }
    public ICommand SearchOnlineLiveTvCommand { get; }
    public ICommand ToggleAddCustomLiveTvCommand { get; }
    public ICommand SaveCustomLiveTvCommand { get; }
    public ICommand DeleteCustomLiveTvChannelCommand { get; }

    // BMKG Cuaca & Gempa Bumi Commands
    public ICommand OpenBmkgCommand { get; }
    public ICommand OpenBmkgWeatherTabCommand { get; }
    public ICommand OpenBmkgEarthquakeTabCommand { get; }
    public ICommand ToggleBmkgWindowCommand { get; }
    public ICommand CloseBmkgWindowCommand { get; }
    public ICommand MinimizeBmkgWindowCommand { get; }
    public ICommand ExpandBmkgWindowCommand { get; }
    public ICommand SetBmkgTabCommand { get; }
    public ICommand SetBmkgEarthquakeSubTabCommand { get; }
    public ICommand RefreshBmkgCommand { get; }
    public ICommand DetectBmkgLocationCommand { get; }
    public ICommand ToggleShakemapCommand { get; }
    public ICommand OpenEarthquakeMapCommand { get; }
    public ICommand OpenShakemapBrowserCommand { get; }
    public ICommand OpenBmkgWebsiteCommand { get; }
    public ICommand ToggleCustomAdm4EditCommand { get; }
    public ICommand ApplyCustomAdm4Command { get; }
    public ICommand TogglePreciseRegionSelectorCommand { get; }
    public ICommand SearchBmkgLocationCommand { get; }
    public ICommand SelectBmkgSearchResultCommand { get; }
    public ICommand ClearBmkgLocationSearchCommand { get; }
    public ICommand TestBmkgAlertCommand { get; }
    public ICommand DismissBmkgNotificationCommand { get; }
    public ICommand OpenBmkgNotificationDetailCommand { get; }

    // Pengingat Pintar (Minum Air, Minum Obat, dll.) Commands
    public ICommand OpenReminderCommand { get; }
    public ICommand ToggleReminderWindowCommand { get; }
    public ICommand CloseReminderWindowCommand { get; }
    public ICommand MinimizeReminderWindowCommand { get; }
    public ICommand ExpandReminderWindowCommand { get; }
    public ICommand AddReminderPresetCommand { get; }
    public ICommand OpenAddReminderFormCommand { get; }
    public ICommand OpenEditReminderFormCommand { get; }
    public ICommand CancelReminderFormCommand { get; }
    public ICommand SaveReminderFormCommand { get; }
    public ICommand SetFormReminderTypeCommand { get; }
    public ICommand SetFormReminderIconCommand { get; }
    public ICommand SetFormIntervalPresetCommand { get; }
    public ICommand ToggleReminderItemCommand { get; }
    public ICommand DeleteReminderItemCommand { get; }
    public ICommand SnoozeReminderItemCommand { get; }
    public ICommand CompleteReminderItemCommand { get; }
    public ICommand ResetReminderTimerCommand { get; }
    public ICommand TestReminderAlertCommand { get; }
    public ICommand DismissReminderNotificationCommand { get; }
    public ICommand SnoozeActiveReminderNotificationCommand { get; }
    public ICommand CompleteActiveReminderNotificationCommand { get; }

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
        IRadioService? radioService = null,
        ILiveTvService? liveTvService = null,
        IBmkgService? bmkgService = null,
        IReminderService? reminderService = null)
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
        _liveTvService = liveTvService ?? new LiveTvService(settingsService);
        _bmkgService = bmkgService ?? new BmkgService(settingsService);
        _reminderService = reminderService ?? new ReminderService(settingsService);

        foreach (var r in _reminderService.Reminders)
        {
            Reminders.Add(r);
            if (r.IsEnabled)
            {
                ActiveReminders.Add(r);
            }
        }
        _reminderService.RemindersUpdated += OnRemindersUpdated;
        _reminderService.ReminderTriggered += OnSmartReminderTriggered;

        _bmkgService.DataUpdated += (s, e) => OnBmkgDataUpdated();
        _bmkgService.NotificationTriggered += (s, evt) => OnBmkgNotificationTriggered(evt);

        foreach (var st in _radioService.Stations)
        {
            RadioStations.Add(st);
        }

        _filteredRadioStations = CollectionViewSource.GetDefaultView(RadioStations);
        _filteredRadioStations.Filter = FilterRadioStation;

        _radioService.StateChanged += OnRadioStateChanged;
        _radioService.StationChanged += _ => OnRadioStateChanged();

        foreach (var country in _liveTvService.Countries)
        {
            LiveTvCountries.Add(country);
        }

        foreach (var ch in _liveTvService.Channels)
        {
            LiveTvChannels.Add(ch);
        }

        _filteredLiveTvChannels = CollectionViewSource.GetDefaultView(LiveTvChannels);
        _filteredLiveTvChannels.Filter = FilterLiveTvChannel;

        _liveTvService.StateChanged += OnLiveTvStateChanged;
        _liveTvService.ChannelChanged += _ => OnLiveTvStateChanged();
        _liveTvService.CatalogUpdated += OnLiveTvCatalogUpdated;

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
        OpenSpotifyCommand = new RelayCommand(() => RequestOpenSpotify?.Invoke());
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

        // Live TV Commands
        OpenLiveTvCommand = new RelayCommand(() =>
        {
            IsLiveTvWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenLiveTv?.Invoke();
        });
        ToggleLiveTvWindowCommand = new RelayCommand(() =>
        {
            IsLiveTvWindowOpen = !IsLiveTvWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsLiveTvWindowOpen)
            {
                RequestOpenLiveTv?.Invoke();
            }
        });
        CloseLiveTvWindowCommand = new RelayCommand(() =>
        {
            _liveTvService.Stop();
            IsLiveTvWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeLiveTvWindowCommand = new RelayCommand(() => IsLiveTvWindowCompact = true);
        ExpandLiveTvWindowCommand = new RelayCommand(() => IsLiveTvWindowCompact = false);
        ToggleLiveTvPlayPauseCommand = new RelayCommand(() => _liveTvService.TogglePlayPause());
        StopLiveTvCommand = new RelayCommand(() => _liveTvService.Stop());
        NextLiveTvChannelCommand = new RelayCommand(() => _liveTvService.NextChannel(FilteredLiveTvChannels.Cast<LiveTvChannel>()));
        PreviousLiveTvChannelCommand = new RelayCommand(() => _liveTvService.PreviousChannel(FilteredLiveTvChannels.Cast<LiveTvChannel>()));
        PlayLiveTvChannelCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch)
            {
                if (IsRadioPlaying || IsRadioBuffering)
                {
                    _radioService.Stop();
                }
                _liveTvService.PlayChannel(ch);
            }
        });
        CycleLiveTvSourceCommand = new RelayCommand(() => _liveTvService.CycleNextSource());
        SetLiveTvCategoryCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                LiveTvCategoryFilter = cat;
            }
        });
        ToggleCurrentLiveTvFavoriteCommand = new RelayCommand(() =>
        {
            if (_liveTvService.CurrentChannel != null)
            {
                _liveTvService.ToggleFavorite(_liveTvService.CurrentChannel);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });
        ToggleLiveTvChannelFavoriteCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch)
            {
                _liveTvService.ToggleFavorite(ch);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });
        ToggleLiveTvMuteCommand = new RelayCommand(() => IsLiveTvMuted = !IsLiveTvMuted);
        SearchOnlineLiveTvCommand = new RelayCommand(async () => await ExecuteOnlineLiveTvSearchAsync());
        ToggleAddCustomLiveTvCommand = new RelayCommand(() => IsAddingCustomLiveTv = !IsAddingCustomLiveTv);
        SaveCustomLiveTvCommand = new RelayCommand(SaveCustomLiveTvChannel);
        DeleteCustomLiveTvChannelCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch && ch.IsCustom)
            {
                _liveTvService.RemoveCustomChannel(ch);
                LiveTvChannels.Remove(ch);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });

        // BMKG Cuaca & Gempa Bumi Commands
        OpenBmkgCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        OpenBmkgWeatherTabCommand = new RelayCommand(() =>
        {
            BmkgActiveTab = "Weather";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        OpenBmkgEarthquakeTabCommand = new RelayCommand(() =>
        {
            BmkgActiveTab = "Earthquake";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        ToggleBmkgWindowCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = !IsBmkgWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsBmkgWindowOpen)
            {
                RequestOpenBmkg?.Invoke();
                _ = _bmkgService.RefreshAllAsync(forceWeather: false);
            }
        });
        CloseBmkgWindowCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeBmkgWindowCommand = new RelayCommand(() => IsBmkgWindowCompact = true);
        ExpandBmkgWindowCommand = new RelayCommand(() => IsBmkgWindowCompact = false);
        SetBmkgTabCommand = new RelayCommand((tabObj) =>
        {
            if (tabObj is string tab && !string.IsNullOrWhiteSpace(tab))
            {
                BmkgActiveTab = tab;
            }
        });
        SetBmkgEarthquakeSubTabCommand = new RelayCommand((subObj) =>
        {
            if (subObj is string sub && !string.IsNullOrWhiteSpace(sub))
            {
                BmkgEarthquakeSubTab = sub;
            }
        });
        RefreshBmkgCommand = new RelayCommand(async () =>
        {
            await _bmkgService.RefreshAllAsync(forceWeather: true);
        });
        DetectBmkgLocationCommand = new RelayCommand(async () => await DetectBmkgLocationAsync());
        ToggleShakemapCommand = new RelayCommand(() => IsShowingShakemap = !IsShowingShakemap);
        OpenEarthquakeMapCommand = new RelayCommand((eqObj) =>
        {
            var eq = eqObj as BmkgEarthquakeItem ?? LatestEarthquake;
            if (eq != null && !string.IsNullOrWhiteSpace(eq.GoogleMapsUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(eq.GoogleMapsUrl) { UseShellExecute = true });
                }
                catch { }
            }
        });
        OpenShakemapBrowserCommand = new RelayCommand(() =>
        {
            if (LatestEarthquake != null && LatestEarthquake.HasShakemap)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(LatestEarthquake.ShakemapUrl) { UseShellExecute = true });
                }
                catch { }
            }
        });
        OpenBmkgWebsiteCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.bmkg.go.id") { UseShellExecute = true });
            }
            catch { }
        });
        ToggleCustomAdm4EditCommand = new RelayCommand(() => IsEditingCustomAdm4 = !IsEditingCustomAdm4);
        ApplyCustomAdm4Command = new RelayCommand(async () =>
        {
            IsEditingCustomAdm4 = false;
            OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            _ = SyncRegionSelectorsFromCurrentAdm4Async();
            await _bmkgService.RefreshWeatherAsync();
        });
        TogglePreciseRegionSelectorCommand = new RelayCommand(() => IsSelectingPreciseRegion = !IsSelectingPreciseRegion);
        SearchBmkgLocationCommand = new RelayCommand(async () => await ExecuteBmkgPreciseLocationSearchAsync());
        SelectBmkgSearchResultCommand = new RelayCommand(async (resObj) =>
        {
            if (resObj is BmkgLocationSearchResult res)
            {
                await ApplyBmkgSearchResultAsync(res);
            }
        });
        ClearBmkgLocationSearchCommand = new RelayCommand(() =>
        {
            BmkgLocationSearchResults.Clear();
            OnPropertyChanged(nameof(HasBmkgLocationSearchResults));
        });
        TestBmkgAlertCommand = new RelayCommand(() => _bmkgService.TriggerTestEarthquakeAlert());
        DismissBmkgNotificationCommand = new RelayCommand(() =>
        {
            _bmkgNotificationDismissTimer?.Stop();
            ActiveBmkgNotification = null;
        });
        OpenBmkgNotificationDetailCommand = new RelayCommand(() =>
        {
            bool isEq = ActiveBmkgNotification?.IsEarthquake ?? true;
            _bmkgNotificationDismissTimer?.Stop();
            ActiveBmkgNotification = null;
            BmkgActiveTab = isEq ? "Earthquake" : "Weather";
            if (isEq) BmkgEarthquakeSubTab = "AutoGempa";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            RequestOpenBmkg?.Invoke();
        });

        // Pengingat Pintar Commands
        OpenReminderCommand = new RelayCommand(() =>
        {
            IsReminderWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenReminder?.Invoke();
        });
        ToggleReminderWindowCommand = new RelayCommand(() =>
        {
            IsReminderWindowOpen = !IsReminderWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsReminderWindowOpen)
            {
                RequestOpenReminder?.Invoke();
            }
        });
        CloseReminderWindowCommand = new RelayCommand(() =>
        {
            IsReminderWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeReminderWindowCommand = new RelayCommand(() => IsReminderWindowCompact = true);
        ExpandReminderWindowCommand = new RelayCommand(() => IsReminderWindowCompact = false);
        AddReminderPresetCommand = new RelayCommand((presetObj) =>
        {
            if (presetObj is string presetId && !string.IsNullOrWhiteSpace(presetId))
            {
                _reminderService.AddFromPreset(presetId);
                SyncRemindersCollection();
            }
            else if (presetObj is ReminderPresetOption preset)
            {
                _reminderService.AddFromPreset(preset.Id);
                SyncRemindersCollection();
            }
        });
        OpenAddReminderFormCommand = new RelayCommand(() =>
        {
            _editingReminderId = null;
            FormReminderTitle = string.Empty;
            FormReminderCategory = "Kesehatan";
            FormReminderIcon = "💧";
            FormReminderNotes = string.Empty;
            FormReminderType = ReminderType.Interval;
            FormReminderIntervalMinutes = 60;
            FormReminderDailyTimesText = "08:00, 13:00, 20:00";
            OnPropertyChanged(nameof(IsEditingExistingReminder));
            IsAddingOrEditingReminder = true;
        });
        OpenEditReminderFormCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _editingReminderId = item.Id;
                FormReminderTitle = item.Title;
                FormReminderCategory = item.Category;
                FormReminderIcon = item.Icon;
                FormReminderNotes = item.Notes;
                FormReminderType = item.Type;
                FormReminderIntervalMinutes = item.IntervalMinutes;
                FormReminderDailyTimesText = item.DailyTimes != null && item.DailyTimes.Count > 0
                    ? string.Join(", ", item.DailyTimes)
                    : "08:00";
                OnPropertyChanged(nameof(IsEditingExistingReminder));
                IsAddingOrEditingReminder = true;
            }
        });
        CancelReminderFormCommand = new RelayCommand(() =>
        {
            _editingReminderId = null;
            IsAddingOrEditingReminder = false;
        });
        SaveReminderFormCommand = new RelayCommand(SaveReminderFromForm);
        SetFormReminderTypeCommand = new RelayCommand((tObj) =>
        {
            if (tObj is string tStr)
            {
                FormReminderType = string.Equals(tStr, "DailyTime", StringComparison.OrdinalIgnoreCase)
                    ? ReminderType.DailyTime
                    : ReminderType.Interval;
            }
            else if (tObj is ReminderType rt)
            {
                FormReminderType = rt;
            }
        });
        SetFormReminderIconCommand = new RelayCommand((iconObj) =>
        {
            if (iconObj is string ic && !string.IsNullOrWhiteSpace(ic))
            {
                FormReminderIcon = ic;
            }
            else if (iconObj is ReminderIconOption opt && !string.IsNullOrWhiteSpace(opt.Key))
            {
                FormReminderIcon = opt.Key;
            }
        });
        SetFormIntervalPresetCommand = new RelayCommand((minObj) =>
        {
            if (minObj is string s && int.TryParse(s, out int m))
            {
                FormReminderIntervalMinutes = m;
            }
            else if (minObj is int mVal)
            {
                FormReminderIntervalMinutes = mVal;
            }
        });
        ToggleReminderItemCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _reminderService.ToggleReminder(item.Id);
            }
        });
        DeleteReminderItemCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _reminderService.DeleteReminder(item.Id);
                SyncRemindersCollection();
            }
        });
        SnoozeReminderItemCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _reminderService.SnoozeReminder(item.Id, 10);
            }
        });
        CompleteReminderItemCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _reminderService.CompleteReminderOccurrence(item.Id);
            }
        });
        ResetReminderTimerCommand = new RelayCommand((itemObj) =>
        {
            if (itemObj is ReminderItem item)
            {
                _reminderService.ResetTimer(item.Id);
            }
        });
        TestReminderAlertCommand = new RelayCommand((itemObj) =>
        {
            _reminderService.TriggerTestNotification(itemObj as ReminderItem);
        });
        DismissReminderNotificationCommand = new RelayCommand(() =>
        {
            _reminderNotificationDismissTimer?.Stop();
            ActiveReminderNotification = null;
        });
        SnoozeActiveReminderNotificationCommand = new RelayCommand((minObj) =>
        {
            int mins = 10;
            if (minObj is string s && int.TryParse(s, out int parsed)) mins = parsed;
            if (ActiveReminderNotification != null && !string.IsNullOrEmpty(ActiveReminderNotification.ReminderId))
            {
                _reminderService.SnoozeReminder(ActiveReminderNotification.ReminderId, mins);
            }
            _reminderNotificationDismissTimer?.Stop();
            ActiveReminderNotification = null;
        });
        CompleteActiveReminderNotificationCommand = new RelayCommand(() =>
        {
            if (ActiveReminderNotification != null && !string.IsNullOrEmpty(ActiveReminderNotification.ReminderId))
            {
                _reminderService.CompleteReminderOccurrence(ActiveReminderNotification.ReminderId);
            }
            _reminderNotificationDismissTimer?.Stop();
            ActiveReminderNotification = null;
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

    public void NotifyYouTubeMusicPlaying()
    {
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
        PlaybackStartedOnYouTubeMusic?.Invoke();
    }

    public void NotifySpotifyPlaying()
    {
        PlaybackStartedOnSpotify?.Invoke();
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
        OnPropertyChanged(nameof(EnableBmkg));
        OnPropertyChanged(nameof(ShowWeatherInCompactNotch));
        OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
        OnPropertyChanged(nameof(ShowHoverWeatherPill));
        OnPropertyChanged(nameof(IsBmkgWindowOpen));
        OnPropertyChanged(nameof(SelectedBmkgCity));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        OnPropertyChanged(nameof(EnableReminders));
        OnPropertyChanged(nameof(ShowReminderInCompactNotch));
        OnPropertyChanged(nameof(ShowHoverReminderPill));
        OnPropertyChanged(nameof(IsReminderWindowOpen));
        OnPropertyChanged(nameof(HasActiveReminderNotification));
        RefreshAdhanSettings();
        _prayerReminderService.RefreshSchedule();
        if (EnableBmkg)
        {
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        }
        if (EnableReminders)
        {
            _reminderService.Start();
        }
        else
        {
            _reminderService.Stop();
            ActiveReminderNotification = null;
        }
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

    // ================= LIVE TV HELPER METHODS =================
    public async Task EnsureInitialLiveTvCatalogLoadedAsync()
    {
        if (_hasLoadedInitialLiveTvCatalog) return;
        _hasLoadedInitialLiveTvCatalog = true;

        try
        {
            await _liveTvService.LoadCountryChannelsAsync("id");
        }
        catch { }
    }

    private async Task LoadSelectedLiveTvCountryAsync(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.Equals(countryCode, "ALL", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _liveTvService.LoadCountryChannelsAsync(countryCode);
        }
        catch { }
    }

    private async Task LoadSelectedLiveTvCategoryIfNeededAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category) ||
            string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Favorites", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Indonesia", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Internasional", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _liveTvService.LoadCategoryChannelsAsync(category, _selectedLiveTvCountryCode);
        }
        catch { }
    }

    private void OnLiveTvCatalogUpdated()
    {
        Action sync = () =>
        {
            var existingIds = LiveTvChannels.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in _liveTvService.Channels)
            {
                if (!existingIds.Contains(ch.Id))
                {
                    LiveTvChannels.Add(ch);
                    existingIds.Add(ch.Id);
                }
            }

            _filteredLiveTvChannels?.Refresh();
            OnPropertyChanged(nameof(LiveTvCatalogCountText));
        };

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(sync);
        }
        else
        {
            sync();
        }
    }

    private void OnLiveTvStateChanged()
    {
        Action notify = () =>
        {
            OnPropertyChanged(nameof(CurrentLiveTvChannel));
            OnPropertyChanged(nameof(CurrentLiveTvChannelName));
            OnPropertyChanged(nameof(CurrentLiveTvChannelSubtitle));
            OnPropertyChanged(nameof(CurrentLiveTvFavoriteSymbol));
            OnPropertyChanged(nameof(CurrentLiveTvSourceBadge));
            OnPropertyChanged(nameof(IsLiveTvPlaying));
            OnPropertyChanged(nameof(IsLiveTvBuffering));
            OnPropertyChanged(nameof(IsLiveTvActiveOrOpen));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsNotchActive));
            OnPropertyChanged(nameof(LiveTvStateBadgeText));
            OnPropertyChanged(nameof(LiveTvStatusMessage));
            OnPropertyChanged(nameof(LiveTvNotchTooltip));
            OnPropertyChanged(nameof(LiveTvVolume));
            OnPropertyChanged(nameof(IsLiveTvMuted));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
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

    private bool FilterLiveTvChannel(object obj)
    {
        if (obj is not LiveTvChannel ch) return false;

        if (!string.IsNullOrWhiteSpace(_selectedLiveTvCountryCode) &&
            !string.Equals(_selectedLiveTvCountryCode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(ch.CountryCode, _selectedLiveTvCountryCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_liveTvCategoryFilter) &&
            !string.Equals(_liveTvCategoryFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(_liveTvCategoryFilter, "Favorites", StringComparison.OrdinalIgnoreCase))
            {
                if (!ch.IsFavorite) return false;
            }
            else if (string.Equals(_liveTvCategoryFilter, "Indonesia", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(ch.CountryCode, "id", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ch.Category, "Indonesia", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (string.Equals(_liveTvCategoryFilter, "Internasional", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(ch.CountryCode, "id", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (!string.Equals(ch.Category, _liveTvCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_liveTvSearchQuery))
        {
            var q = _liveTvSearchQuery.Trim();
            bool match = ch.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.CountryName.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.CountryCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!match) return false;
        }

        return true;
    }

    private async Task ExecuteOnlineLiveTvSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(LiveTvSearchQuery)) return;

        _liveTvSearchCts?.Cancel();
        _liveTvSearchCts?.Dispose();
        _liveTvSearchCts = new CancellationTokenSource();
        var token = _liveTvSearchCts.Token;

        IsSearchingOnlineLiveTv = true;
        try
        {
            var results = await _liveTvService.SearchOnlineChannelsAsync(LiveTvSearchQuery, _selectedLiveTvCountryCode, token);
            if (!token.IsCancellationRequested && results.Count > 0)
            {
                LiveTvCategoryFilter = "All";
                OnLiveTvCatalogUpdated();
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            IsSearchingOnlineLiveTv = false;
        }
    }

    private void SaveCustomLiveTvChannel()
    {
        if (string.IsNullOrWhiteSpace(CustomLiveTvName) || string.IsNullOrWhiteSpace(CustomLiveTvUrl))
            return;

        var url = CustomLiveTvUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var ch = _liveTvService.AddCustomChannel(CustomLiveTvName, url);
        if (!LiveTvChannels.Contains(ch))
        {
            LiveTvChannels.Insert(0, ch);
        }

        CustomLiveTvName = string.Empty;
        CustomLiveTvUrl = string.Empty;
        IsAddingCustomLiveTv = false;
        SelectedLiveTvCountryCode = "ALL";
        LiveTvCategoryFilter = "All";
        _filteredLiveTvChannels?.Refresh();
        OnPropertyChanged(nameof(LiveTvCatalogCountText));
        _liveTvService.PlayChannel(ch);
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
                if (IsLiveTvPlaying || IsLiveTvBuffering)
                {
                    _liveTvService.Stop();
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

    private void OnBmkgDataUpdated()
    {
        void Notify()
        {
            var w = _bmkgService.CurrentWeatherData;
            if (w != null && Settings.BmkgFollowPrayerCity && Math.Abs(w.Latitude) > 0.001 && Math.Abs(w.Longitude) > 0.001)
            {
                bool coordChanged = Math.Abs(Settings.PrayerLatitude - w.Latitude) > 0.002 ||
                                    Math.Abs(Settings.PrayerLongitude - w.Longitude) > 0.002;
                if (coordChanged)
                {
                    _prayerService.UpdateLocation(w.ShortCityLabel, w.Latitude, w.Longitude, w.TimezoneOffsetHours);
                    _prayerReminderService.RefreshSchedule();
                    OnPropertyChanged(nameof(CurrentPrayerSchedule));
                    OnPropertyChanged(nameof(NextPrayerName));
                    OnPropertyChanged(nameof(NextPrayerTimeFormatted));
                    OnPropertyChanged(nameof(NextPrayerCountdownBadge));
                    OnPropertyChanged(nameof(PrayerScheduleTooltip));
                }
            }

            OnPropertyChanged(nameof(CurrentBmkgWeather));
            OnPropertyChanged(nameof(CurrentWeatherSlot));
            OnPropertyChanged(nameof(BmkgWeatherDayGroups));
            OnPropertyChanged(nameof(BmkgWeatherEmoji));
            OnPropertyChanged(nameof(BmkgWeatherIconGeometry));
            OnPropertyChanged(nameof(BmkgWeatherIconColor));
            OnPropertyChanged(nameof(BmkgWeatherTempText));
            OnPropertyChanged(nameof(BmkgWeatherDescText));
            OnPropertyChanged(nameof(BmkgWeatherCityText));
            OnPropertyChanged(nameof(BmkgWeatherFullCityText));
            OnPropertyChanged(nameof(BmkgWeatherLocationSubtitle));
            OnPropertyChanged(nameof(BmkgWeatherRangeText));
            OnPropertyChanged(nameof(BmkgWeatherHumidityText));
            OnPropertyChanged(nameof(BmkgWeatherWindText));
            OnPropertyChanged(nameof(BmkgWeatherCloudText));
            OnPropertyChanged(nameof(BmkgWeatherRainText));
            OnPropertyChanged(nameof(LatestEarthquake));
            OnPropertyChanged(nameof(RecentEarthquakes));
            OnPropertyChanged(nameof(FeltEarthquakes));
            OnPropertyChanged(nameof(HasLatestEarthquake));
            OnPropertyChanged(nameof(LatestEarthquakeBadge));
            OnPropertyChanged(nameof(LatestEarthquakeCompactText));
            OnPropertyChanged(nameof(BmkgStatusMessage));
            OnPropertyChanged(nameof(IsBmkgLoading));
            OnPropertyChanged(nameof(BmkgNotchTooltip));
            OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
            OnPropertyChanged(nameof(ShowHoverWeatherPill));
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(Notify);
        }
        else
        {
            Notify();
        }
    }

    private void OnBmkgNotificationTriggered(BmkgNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActiveBmkgNotification = evt;
            RequestShowTrayNotification?.Invoke(evt.Title, evt.Message);

            _bmkgNotificationDismissTimer?.Stop();
            _bmkgNotificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(22) };
            _bmkgNotificationDismissTimer.Tick += (s, e) =>
            {
                _bmkgNotificationDismissTimer?.Stop();
                ActiveBmkgNotification = null;
            };
            _bmkgNotificationDismissTimer.Start();
        });
    }

    private async Task DetectBmkgLocationAsync()
    {
        BmkgLocationStatus = "Mendeteksi lokasi presisi...";
        try
        {
            var loc = await _prayerService.DetectLocationAsync();
            if (loc != null)
            {
                var precise = await _bmkgService.ResolveCoordinatesToPreciseAdm4Async(loc.Latitude, loc.Longitude, loc.Name);
                if (precise != null && !string.IsNullOrWhiteSpace(precise.Adm4Code))
                {
                    await ApplyBmkgSearchResultAsync(precise);
                    BmkgLocationStatus = $"✓ Terdeteksi presisi: {precise.FullLabel} ({precise.Adm4Code})";
                    return;
                }

                var nearest = CityLocation.FindBestMatchOrNearest(loc.Name, loc.Latitude, loc.Longitude);
                Settings.BmkgSelectedCity = nearest.Name;
                Settings.BmkgCustomAdm4Code = nearest.BmkgAdm4Code;
                if (Settings.BmkgFollowPrayerCity)
                {
                    _prayerService.UpdateLocation(nearest.Name, loc.Latitude, loc.Longitude, nearest.Timezone);
                    _prayerReminderService.RefreshSchedule();
                    OnPropertyChanged(nameof(CurrentPrayerSchedule));
                    OnPropertyChanged(nameof(NextPrayerName));
                    OnPropertyChanged(nameof(NextPrayerTimeFormatted));
                    OnPropertyChanged(nameof(NextPrayerCountdownBadge));
                }
                else
                {
                    _settingsService.Save();
                }

                OnPropertyChanged(nameof(SelectedBmkgCity));
                OnPropertyChanged(nameof(BmkgCustomAdm4Code));
                OnPropertyChanged(nameof(BmkgActiveAdm4Text));
                OnPropertyChanged(nameof(BmkgWeatherCityText));

                BmkgLocationStatus = $"✓ Lokasi: {nearest.DisplayName}";
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
                await _bmkgService.RefreshWeatherAsync();
            }
            else
            {
                BmkgLocationStatus = "Gagal mendeteksi lokasi otomatis. Gunakan pencarian atau pilih wilayah manual.";
            }
        }
        catch
        {
            BmkgLocationStatus = "Gagal mendeteksi lokasi otomatis.";
        }
    }

    private async Task ExecuteBmkgPreciseLocationSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(BmkgLocationSearchQuery))
        {
            BmkgLocationStatus = "Ketik nama Kelurahan, Kecamatan, atau Kota terlebih dahulu.";
            return;
        }

        _bmkgLocationSearchCts?.Cancel();
        _bmkgLocationSearchCts?.Dispose();
        _bmkgLocationSearchCts = new CancellationTokenSource();
        var token = _bmkgLocationSearchCts.Token;

        IsSearchingBmkgLocation = true;
        BmkgLocationStatus = $"Mencari wilayah '{BmkgLocationSearchQuery.Trim()}'...";

        try
        {
            var results = await _bmkgService.SearchPreciseLocationsAsync(BmkgLocationSearchQuery, token);
            if (token.IsCancellationRequested) return;

            BmkgLocationSearchResults.Clear();
            foreach (var r in results)
            {
                BmkgLocationSearchResults.Add(r);
            }
            OnPropertyChanged(nameof(HasBmkgLocationSearchResults));

            if (results.Count == 1)
            {
                await ApplyBmkgSearchResultAsync(results[0]);
            }
            else if (results.Count > 1)
            {
                BmkgLocationStatus = $"Ditemukan {results.Count} wilayah presisi — klik salah satu di bawah untuk menerapkan:";
            }
            else
            {
                BmkgLocationStatus = "Wilayah tidak ditemukan. Gunakan tombol 'Pilih Wilayah' (Provinsi ▸ Kota ▸ Kecamatan ▸ Kelurahan).";
            }
        }
        catch (Exception ex)
        {
            BmkgLocationStatus = $"Pencarian gagal: {ex.Message}";
        }
        finally
        {
            IsSearchingBmkgLocation = false;
        }
    }

    private async Task ApplyBmkgSearchResultAsync(BmkgLocationSearchResult res)
    {
        Settings.BmkgCustomAdm4Code = res.Adm4Code;
        Settings.BmkgSelectedCity = !string.IsNullOrWhiteSpace(res.VillageName)
            ? $"{res.VillageName}, {res.RegencyName}"
            : res.RegencyName;

        if (Settings.BmkgFollowPrayerCity && Math.Abs(res.Latitude) > 0.001 && Math.Abs(res.Longitude) > 0.001)
        {
            _prayerService.UpdateLocation(Settings.BmkgSelectedCity, res.Latitude, res.Longitude, res.Timezone);
            _prayerReminderService.RefreshSchedule();
            OnPropertyChanged(nameof(CurrentPrayerSchedule));
            OnPropertyChanged(nameof(NextPrayerName));
            OnPropertyChanged(nameof(NextPrayerTimeFormatted));
            OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        }

        _settingsService.Save();
        BmkgLocationSearchResults.Clear();
        OnPropertyChanged(nameof(HasBmkgLocationSearchResults));
        OnPropertyChanged(nameof(BmkgCustomAdm4Code));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        OnPropertyChanged(nameof(BmkgWeatherFullCityText));

        BmkgLocationStatus = $"✓ Lokasi presisi: {res.FullLabel} ({res.Adm4Code})";
        _ = SyncRegionSelectorsFromCurrentAdm4Async();
        await _bmkgService.RefreshWeatherAsync();
    }

    public async Task SyncRegionSelectorsFromCurrentAdm4Async()
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;

        try
        {
            string adm4 = _bmkgService.GetActiveAdm4Code();
            var parts = adm4.Split('.');
            string provCode = parts.Length >= 1 ? parts[0] : "31";
            string regCode = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "31.71";
            string distCode = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : "31.71.01";

            var prov = BmkgProvinces.FirstOrDefault(p => p.Code == provCode) ?? BmkgProvinces.FirstOrDefault();
            _selectedBmkgProvince = prov;
            OnPropertyChanged(nameof(SelectedBmkgProvince));

            if (prov != null)
            {
                var regencies = await _bmkgService.GetRegenciesAsync(prov.Code);
                BmkgRegencies.Clear();
                foreach (var r in regencies) BmkgRegencies.Add(r);

                var reg = BmkgRegencies.FirstOrDefault(r => r.Code == regCode) ?? BmkgRegencies.FirstOrDefault();
                _selectedBmkgRegency = reg;
                OnPropertyChanged(nameof(SelectedBmkgRegency));

                if (reg != null)
                {
                    var districts = await _bmkgService.GetDistrictsAsync(reg.Code);
                    BmkgDistricts.Clear();
                    foreach (var d in districts) BmkgDistricts.Add(d);

                    var dist = BmkgDistricts.FirstOrDefault(d => d.Code == distCode) ?? BmkgDistricts.FirstOrDefault();
                    _selectedBmkgDistrict = dist;
                    OnPropertyChanged(nameof(SelectedBmkgDistrict));

                    if (dist != null)
                    {
                        var villages = await _bmkgService.GetVillagesAsync(dist.Code);
                        BmkgVillages.Clear();
                        foreach (var v in villages) BmkgVillages.Add(v);

                        var vil = BmkgVillages.FirstOrDefault(v => v.Code == adm4) ?? BmkgVillages.FirstOrDefault();
                        _selectedBmkgVillage = vil;
                        OnPropertyChanged(nameof(SelectedBmkgVillage));
                    }
                }
            }
        }
        catch { }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedProvinceChangedAsync(string provCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var regencies = await _bmkgService.GetRegenciesAsync(provCode);
            BmkgRegencies.Clear();
            foreach (var r in regencies) BmkgRegencies.Add(r);

            var firstReg = BmkgRegencies.FirstOrDefault();
            _selectedBmkgRegency = firstReg;
            OnPropertyChanged(nameof(SelectedBmkgRegency));

            if (firstReg != null)
            {
                var districts = await _bmkgService.GetDistrictsAsync(firstReg.Code);
                BmkgDistricts.Clear();
                foreach (var d in districts) BmkgDistricts.Add(d);

                var firstDist = BmkgDistricts.FirstOrDefault();
                _selectedBmkgDistrict = firstDist;
                OnPropertyChanged(nameof(SelectedBmkgDistrict));

                if (firstDist != null)
                {
                    var villages = await _bmkgService.GetVillagesAsync(firstDist.Code);
                    BmkgVillages.Clear();
                    foreach (var v in villages) BmkgVillages.Add(v);

                    var firstVil = BmkgVillages.FirstOrDefault();
                    _selectedBmkgVillage = firstVil;
                    OnPropertyChanged(nameof(SelectedBmkgVillage));

                    if (firstVil != null)
                    {
                        ApplyPreciseVillageSelection(firstVil);
                    }
                }
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedRegencyChangedAsync(string regCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var districts = await _bmkgService.GetDistrictsAsync(regCode);
            BmkgDistricts.Clear();
            foreach (var d in districts) BmkgDistricts.Add(d);

            var firstDist = BmkgDistricts.FirstOrDefault();
            _selectedBmkgDistrict = firstDist;
            OnPropertyChanged(nameof(SelectedBmkgDistrict));

            if (firstDist != null)
            {
                var villages = await _bmkgService.GetVillagesAsync(firstDist.Code);
                BmkgVillages.Clear();
                foreach (var v in villages) BmkgVillages.Add(v);

                var firstVil = BmkgVillages.FirstOrDefault();
                _selectedBmkgVillage = firstVil;
                OnPropertyChanged(nameof(SelectedBmkgVillage));

                if (firstVil != null)
                {
                    ApplyPreciseVillageSelection(firstVil);
                }
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedDistrictChangedAsync(string distCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var villages = await _bmkgService.GetVillagesAsync(distCode);
            BmkgVillages.Clear();
            foreach (var v in villages) BmkgVillages.Add(v);

            var firstVil = BmkgVillages.FirstOrDefault();
            _selectedBmkgVillage = firstVil;
            OnPropertyChanged(nameof(SelectedBmkgVillage));

            if (firstVil != null)
            {
                ApplyPreciseVillageSelection(firstVil);
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private void ApplyPreciseVillageSelection(BmkgRegionItem village)
    {
        Settings.BmkgCustomAdm4Code = village.Code;
        string regName = _selectedBmkgRegency?.Name ?? "";
        string distName = _selectedBmkgDistrict?.Name ?? "";
        Settings.BmkgSelectedCity = !string.IsNullOrWhiteSpace(regName) ? $"{village.Name}, {regName}" : village.Name;
        _settingsService.Save();

        OnPropertyChanged(nameof(BmkgCustomAdm4Code));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        OnPropertyChanged(nameof(BmkgWeatherFullCityText));

        BmkgLocationStatus = $"✓ Lokasi presisi: Kel./Desa {village.Name}, Kec. {distName}, {regName} ({village.Code})";
        _ = _bmkgService.RefreshWeatherAsync();
    }

    // ================= PENGINGAT PINTAR HELPER METHODS =================
    private void SyncRemindersCollection()
    {
        var currentList = _reminderService.Reminders.ToList();
        Reminders.Clear();
        foreach (var item in currentList)
        {
            Reminders.Add(item);
        }
        NotifyReminderPropertiesChanged();
    }

    private void OnRemindersUpdated()
    {
        void Notify()
        {
            NotifyReminderPropertiesChanged();
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(Notify);
        }
        else
        {
            Notify();
        }
    }

    private void SyncActiveRemindersCollection()
    {
        var enabledList = Reminders.Where(r => r.IsEnabled).ToList();
        if (!ActiveReminders.SequenceEqual(enabledList))
        {
            ActiveReminders.Clear();
            foreach (var item in enabledList)
            {
                ActiveReminders.Add(item);
            }
        }
    }

    private void NotifyReminderPropertiesChanged()
    {
        SyncActiveRemindersCollection();
        OnPropertyChanged(nameof(NextUpcomingReminder));
        OnPropertyChanged(nameof(HasActiveReminders));
        OnPropertyChanged(nameof(ActiveRemindersCount));
        OnPropertyChanged(nameof(CompletedTodayTotalCount));
        OnPropertyChanged(nameof(ReminderSummaryText));
        OnPropertyChanged(nameof(NextReminderIcon));
        OnPropertyChanged(nameof(NextReminderIconGeometry));
        OnPropertyChanged(nameof(NextReminderAccentColor));
        OnPropertyChanged(nameof(NextReminderBadgeBackground));
        OnPropertyChanged(nameof(NextReminderBadgeBorder));
        OnPropertyChanged(nameof(NextReminderTitle));
        OnPropertyChanged(nameof(NextReminderShortTitle));
        OnPropertyChanged(nameof(NextReminderTimeFormatted));
        OnPropertyChanged(nameof(NextReminderCountdownBadge));
        OnPropertyChanged(nameof(NextReminderScheduleText));
        OnPropertyChanged(nameof(ReminderNotchTooltip));
        OnPropertyChanged(nameof(ShowHoverReminderPill));
    }

    private void OnSmartReminderTriggered(ReminderNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActiveReminderNotification = evt;
            RequestShowTrayNotification?.Invoke(evt.Title, evt.Message);

            _reminderNotificationDismissTimer?.Stop();
            _reminderNotificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(28) };
            _reminderNotificationDismissTimer.Tick += (s, e) =>
            {
                _reminderNotificationDismissTimer?.Stop();
                ActiveReminderNotification = null;
            };
            _reminderNotificationDismissTimer.Start();
        });
    }

    private void SaveReminderFromForm()
    {
        if (string.IsNullOrWhiteSpace(FormReminderTitle))
            return;

        var parsedTimes = new List<string>();
        if (!string.IsNullOrWhiteSpace(FormReminderDailyTimesText))
        {
            var rawTokens = FormReminderDailyTimesText.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var tok in rawTokens)
            {
                var clean = tok.Trim();
                if (TimeSpan.TryParse(clean, out var ts))
                {
                    parsedTimes.Add($"{ts.Hours:D2}:{ts.Minutes:D2}");
                }
            }
        }
        if (parsedTimes.Count == 0)
        {
            parsedTimes.Add("08:00");
        }

        if (!string.IsNullOrEmpty(_editingReminderId))
        {
            var existing = Reminders.FirstOrDefault(r => r.Id == _editingReminderId);
            if (existing != null)
            {
                existing.Title = FormReminderTitle.Trim();
                existing.Category = string.IsNullOrWhiteSpace(FormReminderCategory) ? "Kesehatan" : FormReminderCategory.Trim();
                existing.Icon = string.IsNullOrWhiteSpace(FormReminderIcon) ? "💧" : FormReminderIcon.Trim();
                existing.Notes = FormReminderNotes?.Trim() ?? string.Empty;
                existing.Type = FormReminderType;
                existing.IntervalMinutes = Math.Clamp(FormReminderIntervalMinutes, 1, 1440);
                existing.DailyTimes = parsedTimes;
                existing.IsEnabled = true;
                _reminderService.UpdateReminder(existing);
            }
        }
        else
        {
            var newItem = new ReminderItem
            {
                Title = FormReminderTitle.Trim(),
                Category = string.IsNullOrWhiteSpace(FormReminderCategory) ? "Kesehatan" : FormReminderCategory.Trim(),
                Icon = string.IsNullOrWhiteSpace(FormReminderIcon) ? "💧" : FormReminderIcon.Trim(),
                Notes = FormReminderNotes?.Trim() ?? string.Empty,
                Type = FormReminderType,
                IntervalMinutes = Math.Clamp(FormReminderIntervalMinutes, 1, 1440),
                DailyTimes = parsedTimes,
                IsEnabled = true
            };
            _reminderService.AddReminder(newItem);
            SyncRemindersCollection();
        }

        _editingReminderId = null;
        IsAddingOrEditingReminder = false;
        NotifyReminderPropertiesChanged();
    }

    public async Task InitializeAsync()
    {
        await _mediaService.InitializeAsync();
        _prayerReminderService.Start();
        if (Settings.EnableBmkg)
        {
            _bmkgService.Start();
        }
        if (Settings.EnableReminders)
        {
            _reminderService.Start();
        }

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
        if (IsPinned || IsLyricsMode || IsComponentsMenuOpen || HasActivePrayerNotification || HasActiveBmkgNotification || HasActiveReminderNotification)
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
            if (!_isHoveringWindow && !IsPinned && !IsLyricsMode && !IsComponentsMenuOpen && !HasActivePrayerNotification && !HasActiveBmkgNotification && !HasActiveReminderNotification)
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
