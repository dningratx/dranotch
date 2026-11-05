namespace NotchMusic.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = true;
    public string PreferredDeviceName { get; set; } = string.Empty;
    public double PanelLeft { get; set; } = -1;
    public double PanelTop { get; set; } = -1;
    public double LyricsLeft { get; set; } = -1;
    public double LyricsTop { get; set; } = -1;
    public double LyricsWidth { get; set; } = 480;
    public double LyricsHeight { get; set; } = 320;
    public double LyricsFontSize { get; set; } = 18;
    public double LyricsOpacity { get; set; } = 0.95;
    public bool ExpandOnHover { get; set; } = false;
    public bool IsPinned { get; set; } = false;
    public bool IsBezelMinimized { get; set; } = false;
    public bool AutoMinimizeOnMultipleWindows { get; set; } = true;
    public string PreferredSessionId { get; set; } = string.Empty;
    public bool AutoSearchLyrics { get; set; } = true;
    public bool ClickThroughInLyricsMode { get; set; } = false;
    public string ToggleVisibilityHotkey { get; set; } = "Ctrl+Alt+N";
    public string ToggleLyricsHotkey { get; set; } = "Ctrl+Alt+L";
    public Dictionary<string, double> SongOffsetsSeconds { get; set; } = new();

    // Google Drive Sync
    public string GoogleDriveClientId { get; set; } = string.Empty;
    public string GoogleDriveClientSecret { get; set; } = string.Empty;
    public bool GoogleDriveAutoSync { get; set; } = true;
    public string GoogleDriveUserEmail { get; set; } = string.Empty;
    public DateTime? GoogleDriveLastSyncTime { get; set; }

    // Auto-Update Settings
    public bool AutoCheckUpdates { get; set; } = true;
    public string CustomUpdateUrl { get; set; } = string.Empty;
    public DateTime? LastUpdateCheck { get; set; }
    public string IgnoredUpdateVersion { get; set; } = string.Empty;

    // Component Display & Detach Settings
    public string ComponentDisplayMode { get; set; } = "Both"; // "Both", "MusicOnly", "TodoOnly"
    public bool IsTodoDetached { get; set; } = false;
    public bool IsTodoWindowOpen { get; set; } = true;
    public double TodoWindowLeft { get; set; } = -1;
    public double TodoWindowTop { get; set; } = -1;
    public double TodoWindowWidth { get; set; } = 430;
    public double TodoWindowHeight { get; set; } = 520;
    public bool IsTodoWindowCompact { get; set; } = false;

    // Waktu Sholat & Pengingat (Prayer Times & Reminders)
    public bool EnablePrayerTimes { get; set; } = true;
    public bool IsPrayerDetached { get; set; } = false;
    public bool IsPrayerWindowOpen { get; set; } = true;
    public double PrayerWindowLeft { get; set; } = -1;
    public double PrayerWindowTop { get; set; } = -1;
    public double PrayerWindowWidth { get; set; } = 360;
    public double PrayerWindowHeight { get; set; } = 500;
    public bool IsPrayerWindowCompact { get; set; } = false;
    public string PrayerCity { get; set; } = "Jakarta";
    public double PrayerLatitude { get; set; } = -6.2088;
    public double PrayerLongitude { get; set; } = 106.8456;
    public double PrayerTimezone { get; set; } = 7.0;
    public bool RemindBeforePrayer { get; set; } = true;
    public int RemindBeforeMinutes { get; set; } = 10;
    public bool RemindAtPrayerTime { get; set; } = true;
    public bool RemindAfterPrayer { get; set; } = true;
    public int RemindAfterMinutes { get; set; } = 15;
    public bool PrayerSoundAlert { get; set; } = true;
    public bool PrayerAutoPauseMusic { get; set; } = true;
    public bool ShowPrayerInCompactNotch { get; set; } = true;

    // Pilihan Suara Adzan & Notifikasi
    public string SelectedAdhanSound { get; set; } = "makkah"; // "makkah", "madinah", "alaqsa", "alafasy", "abdulbasit", "mansoor", "refaat", "turkey", "fajr", "chime", "custom"
    public string CustomAdhanSoundPath { get; set; } = string.Empty;
    public double AdhanVolume { get; set; } = 0.85;

    // Pengaturan Durasi Adzan Pendek / Potong Durasi (Menit & Detik Mulai s/d Selesai)
    public bool AdhanCustomDurationEnabled { get; set; } = false;
    public int AdhanStartMinutes { get; set; } = 0;
    public int AdhanStartSeconds { get; set; } = 0;
    public int AdhanEndMinutes { get; set; } = 1;
    public int AdhanEndSeconds { get; set; } = 0;

    public double GetAdhanStartTotalSeconds()
    {
        if (!AdhanCustomDurationEnabled) return 0;
        return Math.Max(0, AdhanStartMinutes) * 60 + Math.Clamp(AdhanStartSeconds, 0, 59);
    }

    public double GetAdhanEndTotalSeconds()
    {
        if (!AdhanCustomDurationEnabled) return 0;
        return Math.Max(0, AdhanEndMinutes) * 60 + Math.Clamp(AdhanEndSeconds, 0, 59);
    }

    // Browser Mengambang (Floating Browser & Video Player)
    public bool IsBrowserWindowOpen { get; set; } = false;
    public double BrowserLeft { get; set; } = -1;
    public double BrowserTop { get; set; } = -1;
    public double BrowserWidth { get; set; } = 960;
    public double BrowserHeight { get; set; } = 540;
    public bool BrowserAlwaysOnTop { get; set; } = true;
    public string BrowserLastUrl { get; set; } = "https://www.youtube.com";

    // Radio Online (Komponen ke-5)
    public bool IsRadioWindowOpen { get; set; } = false;
    public double RadioWindowLeft { get; set; } = -1;
    public double RadioWindowTop { get; set; } = -1;
    public double RadioWindowWidth { get; set; } = 410;
    public double RadioWindowHeight { get; set; } = 540;
    public bool IsRadioWindowCompact { get; set; } = false;
    public double RadioVolume { get; set; } = 0.80;
    public bool RadioMuted { get; set; } = false;
    public string LastRadioStationId { get; set; } = string.Empty;
    public List<string> FavoriteRadioStationIds { get; set; } = new();
    public List<RadioStation> CustomRadioStations { get; set; } = new();
    public bool IsMusicWindowOpen { get; set; } = false;
    public double MusicWindowLeft { get; set; } = -1;
    public double MusicWindowTop { get; set; } = -1;
    public bool IsMusicWindowCompact { get; set; } = false;

    // YouTube Music Window (Browser YouTube Music di Music Player - Default Seukuran Handphone)
    public double YouTubeMusicLeft { get; set; } = -1;
    public double YouTubeMusicTop { get; set; } = -1;
    public double YouTubeMusicWidth { get; set; } = 380;
    public double YouTubeMusicHeight { get; set; } = 740;
    public bool YouTubeMusicAlwaysOnTop { get; set; } = false;

    // Spotify Window (Browser Spotify di Music Player - Default Seukuran Handphone)
    public double SpotifyLeft { get; set; } = -1;
    public double SpotifyTop { get; set; } = -1;
    public double SpotifyWidth { get; set; } = 380;
    public double SpotifyHeight { get; set; } = 740;
    public bool SpotifyAlwaysOnTop { get; set; } = false;

    // Live TV (Komponen ke-6)
    public bool IsLiveTvWindowOpen { get; set; } = false;
    public double LiveTvWindowLeft { get; set; } = -1;
    public double LiveTvWindowTop { get; set; } = -1;
    public double LiveTvWindowWidth { get; set; } = 480;
    public double LiveTvWindowHeight { get; set; } = 640;
    public bool IsLiveTvWindowCompact { get; set; } = false;
    public bool LiveTvAlwaysOnTop { get; set; } = true;
    public double LiveTvVolume { get; set; } = 0.85;
    public bool LiveTvMuted { get; set; } = false;
    public string LastLiveTvChannelId { get; set; } = string.Empty;
    public string LastLiveTvCountryCode { get; set; } = "id";
    public List<string> FavoriteLiveTvChannelIds { get; set; } = new();
    public List<LiveTvChannel> CustomLiveTvChannels { get; set; } = new();

    // Cuaca & Gempa Bumi BMKG (Komponen ke-7)
    public bool EnableBmkg { get; set; } = true;
    public bool IsBmkgWindowOpen { get; set; } = false;
    public double BmkgWindowLeft { get; set; } = -1;
    public double BmkgWindowTop { get; set; } = -1;
    public double BmkgWindowWidth { get; set; } = 440;
    public double BmkgWindowHeight { get; set; } = 600;
    public bool IsBmkgWindowCompact { get; set; } = false;
    public bool ShowWeatherInCompactNotch { get; set; } = true;
    public bool BmkgFollowPrayerCity { get; set; } = true;
    public string BmkgSelectedCity { get; set; } = "Jakarta";
    public string BmkgCustomAdm4Code { get; set; } = string.Empty;
    public bool BmkgNotifyNewEarthquake { get; set; } = true;
    public bool BmkgNotifyExtremeWeather { get; set; } = true;
    public string LastNotifiedEarthquakeId { get; set; } = string.Empty;
    public string LastNotifiedWeatherAlertId { get; set; } = string.Empty;

    // Pengingat Pintar: Minum Air, Minum Obat, & Aktivitas (Komponen ke-8)
    public bool EnableReminders { get; set; } = true;
    public bool IsReminderWindowOpen { get; set; } = false;
    public double ReminderWindowLeft { get; set; } = -1;
    public double ReminderWindowTop { get; set; } = -1;
    public double ReminderWindowWidth { get; set; } = 420;
    public double ReminderWindowHeight { get; set; } = 560;
    public bool IsReminderWindowCompact { get; set; } = false;
    public bool ShowReminderInCompactNotch { get; set; } = true;
    public bool ReminderSoundAlert { get; set; } = true;
    public string SelectedReminderSound { get; set; } = "reflection"; // "reflection", "radar", "marimba", "digital_pulse", "crystal_drop", "morning_breeze", "zen_bowl", "classic_bell", "custom"
    public string CustomReminderSoundPath { get; set; } = string.Empty;
    public double ReminderVolume { get; set; } = 0.85;
}
