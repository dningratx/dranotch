using System.IO;
using System.Text.Json;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.StickyNotes;

public class StickyNoteService : IStickyNoteService
{
    private readonly string _filePath;
    private readonly string _deletedFilePath;
    private readonly object _lock = new();
    private HashSet<string>? _deletedIds;

    public StickyNoteService(ISettingsService settingsService)
    {
        var appDataDir = settingsService.GetAppDataPath();
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "stickynotes.json");
        _deletedFilePath = Path.Combine(appDataDir, "deleted_stickynotes.json");
    }

    public StickyNoteService(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var ext = Path.GetExtension(_filePath);
        _deletedFilePath = !string.IsNullOrEmpty(ext)
            ? Path.ChangeExtension(_filePath, ".deleted" + ext)
            : _filePath + ".deleted.json";
    }

    public List<StickyNoteItem> LoadNotes()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<StickyNoteItem>>(json);
                    if (items != null)
                    {
                        return items;
                    }
                }
            }
            catch
            {
                // Fallback to empty on read or parse failure
            }

            return new List<StickyNoteItem>();
        }
    }

    public void SaveNotes(IEnumerable<StickyNoteItem> notes)
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(notes, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch
            {
                // Suppress save failure to prevent app crash
            }
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
        lock (_lock)
        {
            EnsureDeletedIdsLoaded();
            if (_deletedIds!.Add(id))
            {
                SaveDeletedIds();
            }
        }
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
            if (File.Exists(_deletedFilePath))
            {
                var json = File.ReadAllText(_deletedFilePath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                _deletedIds = list != null ? new HashSet<string>(list) : new HashSet<string>();
                return;
            }
        }
        catch
        {
            // Fallback on corrupt file
        }

        _deletedIds = new HashSet<string>();
    }

    private void SaveDeletedIds()
    {
        try
        {
            if (_deletedIds == null) return;
            var json = JsonSerializer.Serialize(_deletedIds.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var dir = Path.GetDirectoryName(_deletedFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var tempPath = _deletedFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _deletedFilePath, overwrite: true);
        }
        catch
        {
            // Ignore write errors
        }
    }
}
