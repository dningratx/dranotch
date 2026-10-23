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
    private readonly DispatcherTimer _interpolationTimer;

    private PrayerNotificationEvent? _activePrayerNotification;
    private DispatcherTimer? _notificationDismissTimer;
    private bool _isPrayerPanelOpen;

    private readonly ICollectionView _filteredTodos;
    private string _todoFilter = "All";
    private string _newTodoTitle = string.Empty;

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
    private bool _isClickThrough;
    private bool _isHovering;
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
    public event Action? RequestComponentLayoutChanged;
    public event Action<string, string>? RequestShowTrayNotification;

    public PanelMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(IsLyricsMode));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
            }
        }
    }

    public bool IsCompactMode => CurrentMode == PanelMode.Compact;
    public bool IsExpandedMode => CurrentMode == PanelMode.Expanded;
    public bool IsLyricsMode => CurrentMode == PanelMode.Lyrics;

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
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsBrowserClosed => !IsBrowserWindowOpen;
    public string BrowserStatusSummary => IsBrowserWindowOpen ? "Jendela Melayang Aktif" : "Jendela Tertutup";
    public string BrowserButtonActionText => IsBrowserWindowOpen ? "Tutup" : "Buka";

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

    public System.Windows.GridLength MusicColumnWidth => ShowMusicSection ? new System.Windows.GridLength(390) : new System.Windows.GridLength(0);
    public System.Windows.GridLength PrayerColumnWidth => ShowPrayerSectionInMainWindow ? new System.Windows.GridLength(275) : new System.Windows.GridLength(0);
    public System.Windows.GridLength TodoColumnWidth => ShowTodoSectionInMainWindow ? new System.Windows.GridLength(390) : new System.Windows.GridLength(0);

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
                return 440;

            if (ShowMusicSection && ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 1100;
            if (ShowMusicSection && ShowPrayerSectionInMainWindow)
                return 686;
            if (ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 690;
            if (ShowMusicSection && ShowTodoSectionInMainWindow)
                return 805;

            if (ShowMusicSection)
                return 390;
            if (ShowTodoSectionInMainWindow)
                return 390;
            if (ShowPrayerSectionInMainWindow)
                return 275;

            return 390;
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
                OnPropertyChanged(nameof(HasTrack));
            }
        }
    }

    public bool HasTrack => CurrentTrack != null && !string.IsNullOrWhiteSpace(CurrentTrack.Title);

    public bool IsPlaying => CurrentTrack?.IsPlaying ?? false;

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
        set => SetProperty(ref _isPinned, value);
    }

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

    private bool _hasAvailableUpdate;
    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        set => SetProperty(ref _hasAvailableUpdate, value);
    }

    private string _availableUpdateVersion = string.Empty;
    public string AvailableUpdateVersion
    {
        get => _availableUpdateVersion;
        set => SetProperty(ref _availableUpdateVersion, value);
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

    public bool HasActivePrayerNotification => _activePrayerNotification != null;
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
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenYouTubeMusicCommand { get; }
    public ICommand OpenFloatingBrowserCommand { get; }
    public ICommand ImportLyricsCommand { get; }
    public ICommand OffsetAddCommand { get; }
    public ICommand OffsetSubtractCommand { get; }
    public ICommand OffsetResetCommand { get; }
    public ICommand SearchLyricsCommand { get; }
    public ICommand SelectSearchResultCommand { get; }
    public ICommand SelectSessionCommand { get; }
    public ICommand AddTodoCommand { get; }
    public ICommand ClearCompletedTodosCommand { get; }
    public ICommand SetTodoFilterCommand { get; }
    public ICommand SetCategoryFilterCommand { get; }
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
    public ICommand OpenPrayerScheduleCommand { get; }
    public ICommand DismissPrayerNotificationCommand { get; }
    public ICommand ToggleComponentsMenuCommand { get; }
    public ICommand RestoreAllComponentsCommand { get; }
    public ICommand ToggleBrowserCommand { get; }
    public ICommand OpenBrowserCommand { get; }
    public ICommand CloseBrowserCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    public event Action? RequestExit;

    public MainViewModel(
        IMediaSessionService mediaService,
        ILyricsService lyricsService,
        ISettingsService settingsService,
        ITodoService? todoService = null,
        IGoogleDriveSyncService? googleDriveSyncService = null,
        IUpdateService? updateService = null,
        IPrayerService? prayerService = null,
        IPrayerReminderService? prayerReminderService = null)
    {
        _mediaService = mediaService;
        _lyricsService = lyricsService;
        _settingsService = settingsService;
        _todoService = todoService ?? new TodoService(settingsService);
        _googleDriveSyncService = googleDriveSyncService ?? new GoogleDriveSyncService(settingsService);
        _updateService = updateService ?? new UpdateService(settingsService);
        _prayerService = prayerService ?? new PrayerService(settingsService);
        _prayerReminderService = prayerReminderService ?? new PrayerReminderService(_prayerService, settingsService);

        TogglePrayerPanelCommand = new RelayCommand(() => IsPrayerPanelOpen = !IsPrayerPanelOpen);
        OpenPrayerScheduleCommand = new RelayCommand(() =>
        {
            CurrentMode = PanelMode.Expanded;
            IsPrayerPanelOpen = true;
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
            OnPropertyChanged(nameof(NextPrayerName));
            OnPropertyChanged(nameof(NextPrayerTimeFormatted));
            OnPropertyChanged(nameof(NextPrayerCountdownBadge));
            OnPropertyChanged(nameof(PrayerScheduleTooltip));
        };

        OpenUpdateCommand = new RelayCommand(() => RequestOpenSettings?.Invoke());

        _googleDriveSyncService.StatusChanged += () =>
        {
            OnPropertyChanged(nameof(IsGoogleDriveConnected));
            OnPropertyChanged(nameof(IsGoogleDriveSyncing));
            OnPropertyChanged(nameof(GoogleDriveUserEmail));
            OnPropertyChanged(nameof(GoogleDriveSyncStatus));
            OnPropertyChanged(nameof(GoogleDriveStatusButtonText));
            OnPropertyChanged(nameof(GoogleDriveSyncTooltip));
        };
        _googleDriveSyncService.TodosSynced += OnTodosSyncedFromCloud;

        _filteredTodos = CollectionViewSource.GetDefaultView(Todos);
        _filteredTodos.Filter = FilterTodoItem;

        var loadedTodos = _todoService.LoadTodos();
        foreach (var item in loadedTodos)
        {
            var vm = new TodoItemViewModel(item);
            vm.OnChanged += OnTodoItemChanged;
            vm.OnDeleteRequested += DeleteTodo;
            Todos.Add(vm);
        }

        AddTodoCommand = new RelayCommand(AddTodo);
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
                CurrentMode = mode;
            }
            else if (modeObj is string modeStr && Enum.TryParse<PanelMode>(modeStr, true, out var parsed))
            {
                CurrentMode = parsed;
            }
        });

        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        OpenSettingsCommand = new RelayCommand(() => RequestOpenSettings?.Invoke());
        OpenYouTubeMusicCommand = new RelayCommand(() => RequestOpenYouTubeMusic?.Invoke());
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

        _mediaService.TrackChanged += OnTrackChanged;
        _mediaService.TimelineUpdated += OnTimelineUpdated;
        _mediaService.SessionsChanged += (s, list) => OnPropertyChanged(nameof(AvailableSessions));

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
        OnPropertyChanged(nameof(ShowCompactPrayerPill));
        OnPropertyChanged(nameof(CurrentPrayerSchedule));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTimeFormatted));
        OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        OnPropertyChanged(nameof(PrayerScheduleTooltip));
        _prayerReminderService.RefreshSchedule();
        RequestComponentLayoutChanged?.Invoke();
    }

    private void OnPrayerReminderTriggered(PrayerNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActivePrayerNotification = evt;
            RequestShowTrayNotification?.Invoke(evt.Title, evt.Message);

            if (evt.AutoPauseRequested && IsPlaying)
            {
                _ = _mediaService.PauseAsync();
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
            CreatedAt = DateTime.Now
        };

        var vm = new TodoItemViewModel(item);
        vm.OnChanged += OnTodoItemChanged;
        vm.OnDeleteRequested += DeleteTodo;

        Todos.Add(vm);
        NewTodoTitle = string.Empty;

        UpdateTodoCountsAndPersist();
    }

    private void DeleteTodo(TodoItemViewModel item)
    {
        item.OnChanged -= OnTodoItemChanged;
        item.OnDeleteRequested -= DeleteTodo;
        Todos.Remove(item);
        UpdateTodoCountsAndPersist();
    }

    private void ClearCompletedTodos()
    {
        var completed = Todos.Where(t => t.IsCompleted).ToList();
        if (completed.Count == 0) return;

        foreach (var item in completed)
        {
            item.OnChanged -= OnTodoItemChanged;
            item.OnDeleteRequested -= DeleteTodo;
            Todos.Remove(item);
        }
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

    private System.Threading.Timer? _autoSyncDebounceTimer;
    private readonly object _autoSyncLock = new();

    private void ScheduleAutoSync()
    {
        if (!_googleDriveSyncService.IsConnected || !Settings.GoogleDriveAutoSync) return;

        // Snapshot daftar todo pada UI thread untuk memastikan thread safety
        var snapshot = Todos.Select(t => t.ToModel()).ToList();

        lock (_autoSyncLock)
        {
            _autoSyncDebounceTimer?.Dispose();
            _autoSyncDebounceTimer = new System.Threading.Timer(async _ =>
            {
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
            }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }
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

        var merged = await _googleDriveSyncService.SyncAsync(Todos.Select(t => t.ToModel()));
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

        int oldIndex = Todos.IndexOf(source);
        int newIndex = Todos.IndexOf(target);

        if (oldIndex >= 0 && newIndex >= 0)
        {
            Todos.Move(oldIndex, newIndex);
            UpdateTodoCountsAndPersist();
        }
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
    }

    private DispatcherTimer? _expandHoverTimer;

    public void OnMouseEnter()
    {
        _isHovering = true;
        _expandHoverTimer?.Stop();

        // Check for any cloud changes immediately when approaching the notch
        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
        {
            _ = _googleDriveSyncService.CheckForRemoteChangesAsync();
        }

        if (Settings.ExpandOnHover && CurrentMode == PanelMode.Compact && !IsPinned)
        {
            // Tidak langsung terbuka: Berikan jeda hover (650ms) agar tidak terbuka saat kursor hanya melintas
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
        _isHovering = false;
        _expandHoverTimer?.Stop();

        if (Settings.ExpandOnHover && CurrentMode == PanelMode.Expanded && !IsPinned)
        {
            // Delay collapse slightly so it doesn't flicker
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
