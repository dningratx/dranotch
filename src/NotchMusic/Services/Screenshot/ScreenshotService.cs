using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Screenshot;

public class ScreenshotService : IScreenshotService
{
    private readonly ISettingsService? _settingsService;
    private readonly string? _customFolderPath;
    private readonly object _lock = new();
    private List<ScreenshotItem> _screenshots = new();
    private HashSet<string>? _deletedIds;
    private bool _isLoaded;

    public event Action<ScreenshotItem>? ScreenshotCaptured;
    public event Action? ScreenshotsChanged;

    public ScreenshotService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ScreenshotService(string folderPath)
    {
        _customFolderPath = folderPath;
    }

    public string GetScreenshotsFolder()
    {
        if (!string.IsNullOrWhiteSpace(_customFolderPath))
        {
            Directory.CreateDirectory(_customFolderPath);
            return _customFolderPath;
        }

        var customFromSettings = _settingsService?.CurrentSettings.ScreenshotSaveDirectory;
        if (!string.IsNullOrWhiteSpace(customFromSettings) && Directory.Exists(customFromSettings))
        {
            return customFromSettings;
        }

        var appData = _settingsService?.GetAppDataPath() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DRANOTCH");
        var folder = Path.Combine(appData, "Screenshots");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private string GetMetadataFilePath()
    {
        return Path.Combine(GetScreenshotsFolder(), "screenshots.json");
    }

    private string GetDeletedFilePath()
    {
        return Path.Combine(GetScreenshotsFolder(), "deleted_screenshots.json");
    }

    public List<ScreenshotItem> LoadScreenshots()
    {
        lock (_lock)
        {
            if (_isLoaded)
            {
                return _screenshots.ToList();
            }

            var metadataPath = GetMetadataFilePath();
            try
            {
                if (File.Exists(metadataPath))
                {
                    var json = File.ReadAllText(metadataPath);
                    var list = JsonSerializer.Deserialize<List<ScreenshotItem>>(json);
                    if (list != null)
                    {
                        var folder = GetScreenshotsFolder();
                        // Verify files still exist on disk
                        _screenshots = list
                            .Where(item =>
                            {
                                if (File.Exists(item.FilePath)) return true;
                                var fallback = Path.Combine(folder, item.FileName);
                                if (File.Exists(fallback))
                                {
                                    item.FilePath = fallback;
                                    return true;
                                }
                                return false;
                            })
                            .OrderByDescending(x => x.CreatedAt)
                            .ToList();
                        _isLoaded = true;
                        return _screenshots.ToList();
                    }
                }
            }
            catch
            {
                // Fallback to empty on read error
            }

            _screenshots = new List<ScreenshotItem>();
            _isLoaded = true;
            return _screenshots.ToList();
        }
    }

    private void SaveMetadata()
    {
        try
        {
            var metadataPath = GetMetadataFilePath();
            var json = JsonSerializer.Serialize(_screenshots, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var tempPath = metadataPath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(metadataPath))
            {
                File.Replace(tempPath, metadataPath, null);
            }
            else
            {
                File.Move(tempPath, metadataPath);
            }
        }
        catch
        {
            // Ignore metadata write failures
        }
    }

    public ScreenshotItem SaveScreenshot(Bitmap bitmap, string captureMode = "Region", string? customTitle = null)
    {
        lock (_lock)
        {
            LoadScreenshots();

            var folder = GetScreenshotsFolder();
            var timestamp = DateTime.Now;
            var fileName = $"Snip_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..4]}.png";
            var filePath = Path.Combine(folder, fileName);

            bitmap.Save(filePath, ImageFormat.Png);

            var fileInfo = new FileInfo(filePath);
            var title = !string.IsNullOrWhiteSpace(customTitle)
                ? customTitle
                : $"Snip {timestamp:dd/MM/yyyy HH:mm:ss}";

            var item = new ScreenshotItem
            {
                Id = Guid.NewGuid().ToString("N"),
                FileName = fileName,
                FilePath = filePath,
                CreatedAt = timestamp,
                Width = bitmap.Width,
                Height = bitmap.Height,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                Title = title,
                CaptureMode = captureMode,
                IsPinned = false
            };

            _screenshots.Insert(0, item);
            SaveMetadata();

            ScreenshotCaptured?.Invoke(item);
            ScreenshotsChanged?.Invoke();

            return item;
        }
    }

    public ScreenshotItem SaveScreenshot(BitmapSource bitmapSource, string captureMode = "Region", string? customTitle = null)
    {
        lock (_lock)
        {
            LoadScreenshots();

            var folder = GetScreenshotsFolder();
            var timestamp = DateTime.Now;
            var fileName = $"Snip_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..4]}.png";
            var filePath = Path.Combine(folder, fileName);

            ScreenCaptureHelper.SaveBitmapSourceToPng(bitmapSource, filePath);

            var fileInfo = new FileInfo(filePath);
            var title = !string.IsNullOrWhiteSpace(customTitle)
                ? customTitle
                : $"Snip {timestamp:dd/MM/yyyy HH:mm:ss}";

            var item = new ScreenshotItem
            {
                Id = Guid.NewGuid().ToString("N"),
                FileName = fileName,
                FilePath = filePath,
                CreatedAt = timestamp,
                Width = bitmapSource.PixelWidth,
                Height = bitmapSource.PixelHeight,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                Title = title,
                CaptureMode = captureMode,
                IsPinned = false
            };

            _screenshots.Insert(0, item);
            SaveMetadata();

            ScreenshotCaptured?.Invoke(item);
            ScreenshotsChanged?.Invoke();

            return item;
        }
    }

    public bool DeleteScreenshot(string id)
    {
        lock (_lock)
        {
            LoadScreenshots();
            var item = _screenshots.FirstOrDefault(s => s.Id == id);
            if (item == null) return false;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
            }
            catch
            {
                // Ignored
            }

            _screenshots.Remove(item);
            MarkDeleted(id);
            SaveMetadata();

            ScreenshotsChanged?.Invoke();
            return true;
        }
    }

    public void ClearAllScreenshots()
    {
        lock (_lock)
        {
            LoadScreenshots();
            var allIds = _screenshots.Select(s => s.Id).ToList();
            foreach (var item in _screenshots)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                }
                catch { }
            }

            _screenshots.Clear();
            MarkDeleted(allIds);
            SaveMetadata();

            ScreenshotsChanged?.Invoke();
        }
    }

    public IReadOnlySet<string> GetDeletedIds()
    {
        lock (_lock)
        {
            EnsureDeletedIdsLoaded();
            return new HashSet<string>(_deletedIds!);
        }
    }

    public void MarkDeleted(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        MarkDeleted(new[] { id });
    }

    public void MarkDeleted(IEnumerable<string> ids)
    {
        if (ids == null) return;
        lock (_lock)
        {
            EnsureDeletedIdsLoaded();
            bool changed = false;
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id) && _deletedIds!.Add(id))
                {
                    changed = true;
                }
            }
            if (changed)
            {
                SaveDeletedIds();
            }
        }
    }

    public void ClearDeletedIds(IEnumerable<string> idsToClear)
    {
        if (idsToClear == null) return;
        lock (_lock)
        {
            EnsureDeletedIdsLoaded();
            bool changed = false;
            foreach (var id in idsToClear)
            {
                if (_deletedIds!.Remove(id))
                {
                    changed = true;
                }
            }
            if (changed)
            {
                SaveDeletedIds();
            }
        }
    }

    private void EnsureDeletedIdsLoaded()
    {
        if (_deletedIds != null) return;

        try
        {
            var path = GetDeletedFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                _deletedIds = list != null ? new HashSet<string>(list) : new HashSet<string>();
                return;
            }
        }
        catch
        {
            // Ignore
        }

        _deletedIds = new HashSet<string>();
    }

    private void SaveDeletedIds()
    {
        try
        {
            if (_deletedIds == null) return;
            var path = GetDeletedFilePath();
            var json = JsonSerializer.Serialize(_deletedIds.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // Suppress save failure
        }
    }

    public void SetScreenshots(IEnumerable<ScreenshotItem> items)
    {
        lock (_lock)
        {
            _screenshots = items.OrderByDescending(s => s.CreatedAt).ToList();
            _isLoaded = true;
            SaveMetadata();
            ScreenshotsChanged?.Invoke();
        }
    }

    public void UpdateScreenshot(ScreenshotItem item)
    {
        lock (_lock)
        {
            LoadScreenshots();
            var existing = _screenshots.FirstOrDefault(s => s.Id == item.Id);
            if (existing != null)
            {
                existing.Title = item.Title;
                existing.Note = item.Note;
                existing.IsPinned = item.IsPinned;

                SaveMetadata();
                ScreenshotsChanged?.Invoke();
            }
        }
    }

    public int ExportAll(string targetDirectory)
    {
        lock (_lock)
        {
            LoadScreenshots();
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            int count = 0;
            foreach (var item in _screenshots)
            {
                if (File.Exists(item.FilePath))
                {
                    try
                    {
                        var dest = Path.Combine(targetDirectory, item.FileName);
                        File.Copy(item.FilePath, dest, overwrite: true);
                        count++;
                    }
                    catch { }
                }
            }

            return count;
        }
    }
}
