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
}
