using NotchMusic.Models;

namespace NotchMusic.Services.Sync;

public interface IGoogleDriveSyncService
{
    bool IsConfigured { get; }
    bool IsConnected { get; }
    bool IsSyncing { get; }
    string UserEmail { get; }
    DateTime? LastSyncTime { get; }
    string SyncStatus { get; }

    event Action? StatusChanged;
    event Action<List<TodoItem>>? TodosSynced;

    Task<bool> AuthenticateAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, IEnumerable<string>? deletedIds = null, CancellationToken ct = default);
    Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default);
    Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default);
    Task<List<TodoItem>?> CheckForRemoteChangesAsync(CancellationToken ct = default);
    bool ImportCredentialsFile(string filePath);
}
