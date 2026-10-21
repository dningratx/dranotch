using System.IO;
using System.Text.Json;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Todo;

public class TodoService : ITodoService
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public TodoService(ISettingsService settingsService)
    {
        var appDataDir = settingsService.GetAppDataPath();
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "todos.json");
    }

    public TodoService(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public List<TodoItem> LoadTodos()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<TodoItem>>(json);
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

            return new List<TodoItem>();
        }
    }

    public void SaveTodos(IEnumerable<TodoItem> todos)
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(todos, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Write to temp file then replace for atomic update
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
}
