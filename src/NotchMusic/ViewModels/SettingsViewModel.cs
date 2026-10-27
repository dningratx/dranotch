using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Win32;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.Services.Update;

namespace NotchMusic.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IGoogleDriveSyncService _syncService;
    private readonly IUpdateService _updateService;
    private readonly IPrayerService _prayerService;
    private readonly Action<string> _onHotkeyUpdated;

    public AppSettings Settings => _settingsService.CurrentSettings;

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows != value)
            {
                _settingsService.SetStartWithWindows(value);
                OnPropertyChanged();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set
        {
            if (Settings.AlwaysOnTop != value)
            {
                Settings.AlwaysOnTop = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool ExpandOnHover
    {
        get => Settings.ExpandOnHover;
        set
        {
            if (Settings.ExpandOnHover != value)
            {
                Settings.ExpandOnHover = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string ComponentDisplayMode
    {
        get => Settings.ComponentDisplayMode ?? "Both";
        set
        {
            if (Settings.ComponentDisplayMode != value)
            {
                Settings.ComponentDisplayMode = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsComponentBoth));
                OnPropertyChanged(nameof(IsComponentMusicOnly));
                OnPropertyChanged(nameof(IsComponentTodoOnly));
            }
        }
    }

    public bool IsComponentBoth
    {
        get => ComponentDisplayMode == "Both";
        set
        {
            if (value) ComponentDisplayMode = "Both";
        }
    }

    public bool IsComponentMusicOnly
    {
        get => ComponentDisplayMode == "MusicOnly";
        set
        {
            if (value) ComponentDisplayMode = "MusicOnly";
        }
    }

    public bool IsComponentTodoOnly
    {
        get => ComponentDisplayMode == "TodoOnly";
        set
        {
            if (value) ComponentDisplayMode = "TodoOnly";
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
                OnPropertyChanged();
            }
        }
    }

    public bool AutoSearchLyrics
    {
        get => Settings.AutoSearchLyrics;
        set
        {
            if (Settings.AutoSearchLyrics != value)
            {
                Settings.AutoSearchLyrics = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double LyricsFontSize
    {
        get => Settings.LyricsFontSize;
        set
        {
            if (Math.Abs(Settings.LyricsFontSize - value) > 0.1)
            {
                Settings.LyricsFontSize = Math.Clamp(value, 12, 36);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double LyricsOpacity
    {
        get => Settings.LyricsOpacity;
        set
        {
            if (Math.Abs(Settings.LyricsOpacity - value) > 0.01)
            {
                Settings.LyricsOpacity = Math.Clamp(value, 0.2, 1.0);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string ToggleVisibilityHotkey
    {
        get => Settings.ToggleVisibilityHotkey;
        set
        {
            if (Settings.ToggleVisibilityHotkey != value)
            {
                Settings.ToggleVisibilityHotkey = value;
                _settingsService.Save();
                OnPropertyChanged();
                _onHotkeyUpdated?.Invoke(value);
            }
        }
    }

    public string ToggleLyricsHotkey
    {
        get => Settings.ToggleLyricsHotkey;
        set
        {
            if (Settings.ToggleLyricsHotkey != value)
            {
                Settings.ToggleLyricsHotkey = value;
                _settingsService.Save();
                OnPropertyChanged();
                _onHotkeyUpdated?.Invoke(value);
            }
        }
    }

    public string CachePath => _settingsService.GetLyricsCachePath();

    // Google Drive Sync Properties
    public string GoogleDriveClientId
    {
        get => Settings.GoogleDriveClientId;
        set
        {
            if (Settings.GoogleDriveClientId != value)
            {
                Settings.GoogleDriveClientId = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            }
        }
    }

    public string GoogleDriveClientSecret
    {
        get => Settings.GoogleDriveClientSecret;
        set
        {
            if (Settings.GoogleDriveClientSecret != value)
            {
                Settings.GoogleDriveClientSecret = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            }
        }
    }

    public bool GoogleDriveAutoSync
    {
        get => Settings.GoogleDriveAutoSync;
        set
        {
            if (Settings.GoogleDriveAutoSync != value)
            {
                Settings.GoogleDriveAutoSync = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool IsGoogleDriveConnected => _syncService.IsConnected;
    public bool IsGoogleDriveConfigured => _syncService.IsConfigured;
    public bool IsGoogleDriveSyncing => _syncService.IsSyncing;
    public string GoogleDriveUserEmail => _syncService.UserEmail;
    public string GoogleDriveSyncStatus => _syncService.SyncStatus;
    public string GoogleDriveLastSyncFormatted =>
        Settings.GoogleDriveLastSyncTime.HasValue
            ? Settings.GoogleDriveLastSyncTime.Value.ToString("dd MMM yyyy, HH:mm")
            : "Belum pernah";

    private bool _showCustomCredentials;
    public bool ShowCustomCredentials
    {
        get => _showCustomCredentials;
        set => SetProperty(ref _showCustomCredentials, value);
    }

    public string CurrentVersion => _updateService.CurrentVersion;

    public bool AutoCheckUpdates
    {
        get => Settings.AutoCheckUpdates;
        set
        {
            if (Settings.AutoCheckUpdates != value)
            {
                Settings.AutoCheckUpdates = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string CustomUpdateUrl
    {
        get => Settings.CustomUpdateUrl;
        set
        {
            if (Settings.CustomUpdateUrl != value)
            {
                Settings.CustomUpdateUrl = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    private bool _isCheckingUpdate;
    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        set => SetProperty(ref _isCheckingUpdate, value);
    }

    private bool _isDownloadingUpdate;
    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => SetProperty(ref _isDownloadingUpdate, value);
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    private string _updateStatus = string.Empty;
    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    private bool _hasAvailableUpdate;
    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        set => SetProperty(ref _hasAvailableUpdate, value);
    }

    private UpdateInfo? _availableUpdate;
    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        set => SetProperty(ref _availableUpdate, value);
    }

    private bool _showCustomUpdateUrl;
    public bool ShowCustomUpdateUrl
    {
        get => _showCustomUpdateUrl;
        set => SetProperty(ref _showCustomUpdateUrl, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Waktu Sholat & Pengingat Properties
    public bool EnablePrayerTimes
    {
        get => Settings.EnablePrayerTimes;
        set
        {
            if (Settings.EnablePrayerTimes != value)
            {
                Settings.EnablePrayerTimes = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public List<CityLocation> IndonesianCities { get; } = CityLocation.GetIndonesianCities();

    public CityLocation? SelectedCity
    {
        get => IndonesianCities.FirstOrDefault(c => c.Name.Equals(Settings.PrayerCity, StringComparison.OrdinalIgnoreCase))
               ?? IndonesianCities.FirstOrDefault();
        set
        {
            if (value != null && Settings.PrayerCity != value.Name)
            {
                Settings.PrayerCity = value.Name;
                Settings.PrayerLatitude = value.Latitude;
                Settings.PrayerLongitude = value.Longitude;
                Settings.PrayerTimezone = value.Timezone;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerCity));
                OnPropertyChanged(nameof(PrayerLatitude));
                OnPropertyChanged(nameof(PrayerLongitude));
                OnPropertyChanged(nameof(PrayerTimezone));
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public string PrayerCity
    {
        get => Settings.PrayerCity;
        set
        {
            if (Settings.PrayerCity != value)
            {
                Settings.PrayerCity = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCity));
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerLatitude
    {
        get => Settings.PrayerLatitude;
        set
        {
            if (Math.Abs(Settings.PrayerLatitude - value) > 0.0001)
            {
                Settings.PrayerLatitude = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerLongitude
    {
        get => Settings.PrayerLongitude;
        set
        {
            if (Math.Abs(Settings.PrayerLongitude - value) > 0.0001)
            {
                Settings.PrayerLongitude = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerTimezone
    {
        get => Settings.PrayerTimezone;
        set
        {
            if (Math.Abs(Settings.PrayerTimezone - value) > 0.01)
            {
                Settings.PrayerTimezone = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public bool RemindBeforePrayer
    {
        get => Settings.RemindBeforePrayer;
        set
        {
            if (Settings.RemindBeforePrayer != value)
            {
                Settings.RemindBeforePrayer = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public int RemindBeforeMinutes
    {
        get => Settings.RemindBeforeMinutes;
        set
        {
            if (Settings.RemindBeforeMinutes != value)
            {
                Settings.RemindBeforeMinutes = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public List<int> AvailableMinutesOptions { get; } = new() { 5, 10, 15, 20, 30 };

    public bool RemindAtPrayerTime
    {
        get => Settings.RemindAtPrayerTime;
        set
        {
            if (Settings.RemindAtPrayerTime != value)
            {
                Settings.RemindAtPrayerTime = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool RemindAfterPrayer
    {
        get => Settings.RemindAfterPrayer;
        set
        {
            if (Settings.RemindAfterPrayer != value)
            {
                Settings.RemindAfterPrayer = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public int RemindAfterMinutes
    {
        get => Settings.RemindAfterMinutes;
        set
        {
            if (Settings.RemindAfterMinutes != value)
            {
                Settings.RemindAfterMinutes = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool PrayerSoundAlert
    {
        get => Settings.PrayerSoundAlert;
        set
        {
            if (Settings.PrayerSoundAlert != value)
            {
                Settings.PrayerSoundAlert = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool PrayerAutoPauseMusic
    {
        get => Settings.PrayerAutoPauseMusic;
        set
        {
            if (Settings.PrayerAutoPauseMusic != value)
            {
                Settings.PrayerAutoPauseMusic = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowPrayerInCompactNotch
    {
        get => Settings.ShowPrayerInCompactNotch;
        set
        {
            if (Settings.ShowPrayerInCompactNotch != value)
            {
                Settings.ShowPrayerInCompactNotch = value;
                _settingsService.Save();
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }
    }

    public PrayerSchedule TodayPrayerSchedule => _prayerService.GetTodaySchedule();

    private bool _isDetectingLocation;
    public bool IsDetectingLocation
    {
        get => _isDetectingLocation;
        set => SetProperty(ref _isDetectingLocation, value);
    }

    private string _locationDetectStatus = string.Empty;
    public string LocationDetectStatus
    {
        get => _locationDetectStatus;
        set => SetProperty(ref _locationDetectStatus, value);
    }

    private string _testNotificationStatus = string.Empty;
    public string TestNotificationStatus
    {
        get => _testNotificationStatus;
        set => SetProperty(ref _testNotificationStatus, value);
    }

    public ICommand ClearCacheCommand { get; }
    public ICommand ConnectGoogleDriveCommand { get; }
    public ICommand DisconnectGoogleDriveCommand { get; }
    public ICommand ImportCredentialsCommand { get; }
    public ICommand SyncGoogleDriveNowCommand { get; }
    public ICommand ToggleCustomCredentialsCommand { get; }
    public ICommand ToggleCustomUpdateUrlCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand DownloadAndInstallUpdateCommand { get; }
    public ICommand OpenReleasePageCommand { get; }
    public ICommand DetectLocationCommand { get; }
    public ICommand TestPrayerChimeCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        Action<string> onHotkeyUpdated,
        IGoogleDriveSyncService? syncService = null,
        IUpdateService? updateService = null,
        IPrayerService? prayerService = null)
    {
        _settingsService = settingsService;
        _onHotkeyUpdated = onHotkeyUpdated;
        _syncService = syncService ?? new GoogleDriveSyncService(settingsService);
        _updateService = updateService ?? new UpdateService(settingsService);
        _prayerService = prayerService ?? new PrayerService(settingsService);

        DetectLocationCommand = new RelayCommand(async () =>
        {
            if (IsDetectingLocation) return;
            IsDetectingLocation = true;
            LocationDetectStatus = "Mendeteksi lokasi via IP...";

            try
            {
                var detected = await _prayerService.DetectLocationAsync();
                if (detected != null)
                {
                    Settings.PrayerCity = detected.Name;
                    Settings.PrayerLatitude = detected.Latitude;
                    Settings.PrayerLongitude = detected.Longitude;
                    Settings.PrayerTimezone = detected.Timezone;
                    _settingsService.Save();

                    OnPropertyChanged(nameof(PrayerCity));
                    OnPropertyChanged(nameof(SelectedCity));
                    OnPropertyChanged(nameof(PrayerLatitude));
                    OnPropertyChanged(nameof(PrayerLongitude));
                    OnPropertyChanged(nameof(PrayerTimezone));
                    OnPropertyChanged(nameof(TodayPrayerSchedule));

                    LocationDetectStatus = $"Lokasi terdeteksi: {detected.Name} ({detected.Latitude:F2}, {detected.Longitude:F2})";
                }
                else
                {
                    LocationDetectStatus = "Gagal mendeteksi lokasi otomatis. Silakan pilih kota manual.";
                }
            }
            catch (Exception ex)
            {
                LocationDetectStatus = $"Gagal mendeteksi lokasi: {ex.Message}";
            }
            finally
            {
                IsDetectingLocation = false;
            }
        });

        TestPrayerChimeCommand = new RelayCommand(() =>
        {
            PrayerAudioHelper.PlayChime();
            TestNotificationStatus = "Suara pengingat sholat berbunyi ✓";
        });

        ToggleCustomCredentialsCommand = new RelayCommand(() => ShowCustomCredentials = !ShowCustomCredentials);
        ToggleCustomUpdateUrlCommand = new RelayCommand(() => ShowCustomUpdateUrl = !ShowCustomUpdateUrl);

        CheckUpdateCommand = new RelayCommand(async () =>
        {
            if (IsCheckingUpdate || IsDownloadingUpdate) return;
            IsCheckingUpdate = true;
            UpdateStatus = "Mengecek pembaruan aplikasi...";
            HasAvailableUpdate = false;
            AvailableUpdate = null;

            try
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null && update.IsNewerVersion)
                {
                    AvailableUpdate = update;
                    HasAvailableUpdate = true;
                    UpdateStatus = $"Tersedia versi baru: v{update.Version} ({update.UpdateTypeLabel})";
                }
                else if (update != null)
                {
                    UpdateStatus = $"Aplikasi Anda sudah versi terbaru (v{CurrentVersion}).";
                }
                else
                {
                    UpdateStatus = "Tidak dapat memeriksa pembaruan saat ini. Periksa koneksi internet Anda.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Gagal mengecek pembaruan: {ex.Message}";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        });

        DownloadAndInstallUpdateCommand = new RelayCommand(async () =>
        {
            if (AvailableUpdate == null) return;

            if (string.IsNullOrWhiteSpace(AvailableUpdate.DownloadUrl))
            {
                if (!string.IsNullOrWhiteSpace(AvailableUpdate.ReleasePageUrl))
                {
                    try { Process.Start(new ProcessStartInfo(AvailableUpdate.ReleasePageUrl) { UseShellExecute = true }); } catch { }
                }
                return;
            }

            IsDownloadingUpdate = true;
            DownloadProgress = 0;
            UpdateStatus = "Mengunduh file pembaruan...";

            try
            {
                var progress = new Progress<double>(pct => DownloadProgress = pct);
                var installerPath = await _updateService.DownloadUpdateAsync(AvailableUpdate.DownloadUrl, progress);
                UpdateStatus = "Download selesai. Menjalankan installer...";
                await Task.Delay(500);
                _updateService.LaunchInstallerAndExit(installerPath);
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Gagal mengunduh installer: {ex.Message}";
                IsDownloadingUpdate = false;
            }
        });

        OpenReleasePageCommand = new RelayCommand(() =>
        {
            var url = AvailableUpdate?.ReleasePageUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "https://github.com/dranotch/dranotch/releases";
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        });

        _syncService.StatusChanged += () =>
        {
            OnPropertyChanged(nameof(IsGoogleDriveConnected));
            OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            OnPropertyChanged(nameof(IsGoogleDriveSyncing));
            OnPropertyChanged(nameof(GoogleDriveUserEmail));
            OnPropertyChanged(nameof(GoogleDriveSyncStatus));
            OnPropertyChanged(nameof(GoogleDriveLastSyncFormatted));
        };

        ClearCacheCommand = new RelayCommand(() =>
        {
            _settingsService.ClearLyricsCache();
            StatusMessage = "Cache lirik berhasil dibersihkan.";
        });

        ConnectGoogleDriveCommand = new RelayCommand(async () =>
        {
            StatusMessage = "Menghubungkan akun Google...";
            var success = await _syncService.AuthenticateAsync();
            StatusMessage = success
                ? $"Berhasil terhubung ke Google Drive sebagai {_syncService.UserEmail}!"
                : $"Gagal terhubung: {_syncService.SyncStatus}";
        });

        DisconnectGoogleDriveCommand = new RelayCommand(async () =>
        {
            await _syncService.DisconnectAsync();
            StatusMessage = "Sambungan Google Drive diputuskan.";
        });

        ImportCredentialsCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Pilih file credentials.json dari Google Cloud Console"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_syncService.ImportCredentialsFile(dialog.FileName))
                {
                    OnPropertyChanged(nameof(GoogleDriveClientId));
                    OnPropertyChanged(nameof(GoogleDriveClientSecret));
                    OnPropertyChanged(nameof(IsGoogleDriveConfigured));
                    StatusMessage = "Kredensial Google Drive berhasil diimpor!";
                }
                else
                {
                    StatusMessage = "Format file credentials.json tidak valid.";
                }
            }
        });

        SyncGoogleDriveNowCommand = new RelayCommand(async () =>
        {
            StatusMessage = "Menyinkronkan dengan Google Drive...";
            var todoService = new TodoService(_settingsService);
            var todos = todoService.LoadTodos();
            var deletedIds = todoService.GetDeletedIds();
            var merged = await _syncService.SyncAsync(todos, deletedIds);
            if (merged != null)
            {
                todoService.SaveTodos(merged);
                StatusMessage = "Sinkronisasi Google Drive berhasil!";
            }
            else
            {
                StatusMessage = $"Sinkronisasi gagal: {_syncService.SyncStatus}";
            }
        });

        ExitApplicationCommand = new RelayCommand(() =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                {
                    mw.ExitApplication();
                }
                else
                {
                    System.Windows.Application.Current.Shutdown();
                }
            });
        });
    }
}
