using System.IO;
using System.Text.Json;
using NotchMusic.Models;

namespace NotchMusic.Services.Settings;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    void Save();
    void SetStartWithWindows(bool enable);
    string GetAppDataPath();
    string GetLyricsCachePath();
    void ClearLyricsCache();
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _appDataDir;
    private readonly string _lyricsCacheDir;
    private AppSettings _settings;

    public AppSettings CurrentSettings => _settings;

    public SettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appDataDir = Path.Combine(localAppData, "DRANOTCH");
        var legacyDir = Path.Combine(localAppData, "NOTCHMusic");
        if (!Directory.Exists(_appDataDir) && Directory.Exists(legacyDir))
        {
            try { Directory.Move(legacyDir, _appDataDir); } catch { }
        }
        Directory.CreateDirectory(_appDataDir);

        _lyricsCacheDir = Path.Combine(_appDataDir, "LyricsCache");
        Directory.CreateDirectory(_lyricsCacheDir);

        _settingsFilePath = Path.Combine(_appDataDir, "settings.json");
        _settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fallback to default
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Handle or log if needed
        }
    }

    public void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                var exePath = Environment.ProcessPath;
                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    key.SetValue("DRANOTCH", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("DRANOTCH", false);
                }
            }
            _settings.StartWithWindows = enable;
            Save();
        }
        catch
        {
            // Silently fail if registry is blocked
        }
    }

    public string GetAppDataPath() => _appDataDir;
    public string GetLyricsCachePath() => _lyricsCacheDir;

    public void ClearLyricsCache()
    {
        try
        {
            if (Directory.Exists(_lyricsCacheDir))
            {
                var files = Directory.GetFiles(_lyricsCacheDir);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
        }
    }
}
