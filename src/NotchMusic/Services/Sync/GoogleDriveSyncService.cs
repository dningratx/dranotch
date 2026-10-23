using System.IO;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Sync;

public class GoogleDriveSyncService : IGoogleDriveSyncService
{
    private const string FolderName = "DRANOTCH";
    private const string FileName = "todos.json";
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

    // Kredensial Bawaan Aplikasi DRANOTCH
    private static readonly byte[] EncId = new byte[] { 105, 107, 107, 109, 108, 109, 109, 110, 106, 111, 108, 107, 119, 55, 63, 106, 57, 43, 44, 52, 46, 40, 49, 40, 104, 106, 47, 106, 104, 43, 56, 42, 55, 60, 98, 106, 60, 56, 105, 59, 44, 41, 44, 98, 40, 116, 59, 42, 42, 41, 116, 61, 53, 53, 61, 54, 63, 47, 41, 63, 40, 57, 53, 52, 46, 63, 52, 46, 116, 57, 53, 55 };
    private static readonly byte[] EncSec = new byte[] { 29, 21, 25, 9, 10, 2, 119, 52, 48, 2, 56, 57, 34, 104, 56, 111, 12, 10, 99, 16, 9, 40, 30, 22, 21, 23, 9, 28, 14, 119, 31, 45, 19, 16, 13 };

    public static readonly string DefaultClientId = DecodeCred(EncId);
    public static readonly string DefaultClientSecret = DecodeCred(EncSec);

    private static string DecodeCred(byte[] data)
    {
        var b = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) b[i] = (byte)(data[i] ^ 0x5A);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    private readonly ISettingsService _settingsService;
    private readonly string _authDataStorePath;
    private readonly object _syncLock = new();

    private bool _isSyncing;
    private string _syncStatus = "Belum terhubung";

    public string EffectiveClientId =>
        !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.GoogleDriveClientId)
            ? _settingsService.CurrentSettings.GoogleDriveClientId.Trim()
            : DefaultClientId;

    public string EffectiveClientSecret =>
        !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.GoogleDriveClientSecret)
            ? _settingsService.CurrentSettings.GoogleDriveClientSecret.Trim()
            : DefaultClientSecret;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(EffectiveClientId) &&
        !string.IsNullOrWhiteSpace(EffectiveClientSecret);

    public bool IsConnected =>
        IsConfigured &&
        !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.GoogleDriveUserEmail);

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            _isSyncing = value;
            StatusChanged?.Invoke();
        }
    }

    public string UserEmail => _settingsService.CurrentSettings.GoogleDriveUserEmail;
    public DateTime? LastSyncTime => _settingsService.CurrentSettings.GoogleDriveLastSyncTime;
    public string SyncStatus => _syncStatus;

    public event Action? StatusChanged;
    public event Action<List<TodoItem>>? TodosSynced;

    public GoogleDriveSyncService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _authDataStorePath = Path.Combine(_settingsService.GetAppDataPath(), "GoogleAuthDataStore");

        if (IsConnected)
        {
            _syncStatus = $"Tersambung ({UserEmail})";
        }
        else if (IsConfigured)
        {
            _syncStatus = "Siap login";
        }
    }

    public bool ImportCredentialsFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement clientEl;
            if (root.TryGetProperty("installed", out clientEl) || root.TryGetProperty("web", out clientEl))
            {
                if (clientEl.TryGetProperty("client_id", out var idProp) &&
                    clientEl.TryGetProperty("client_secret", out var secretProp))
                {
                    _settingsService.CurrentSettings.GoogleDriveClientId = idProp.GetString() ?? string.Empty;
                    _settingsService.CurrentSettings.GoogleDriveClientSecret = secretProp.GetString() ?? string.Empty;
                    _settingsService.Save();
                    _syncStatus = "Kredensial disimpan. Siap login.";
                    StatusChanged?.Invoke();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal impor kredensial: {ex.Message}";
            StatusChanged?.Invoke();
        }
        return false;
    }

    private async Task<UserCredential?> GetCredentialAsync(CancellationToken ct)
    {
        var clientId = EffectiveClientId;
        var clientSecret = EffectiveClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _syncStatus = "Client ID / Secret belum tersedia";
            StatusChanged?.Invoke();
            return null;
        }

        Directory.CreateDirectory(_authDataStorePath);
        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            ct,
            new FileDataStore(_authDataStorePath, true));
    }

    private async Task<DriveService?> CreateDriveServiceAsync(CancellationToken ct)
    {
        var credential = await GetCredentialAsync(ct);
        if (credential == null) return null;

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DRANOTCH"
        });
    }

    public async Task<bool> AuthenticateAsync(CancellationToken ct = default)
    {
        try
        {
            IsSyncing = true;
            _syncStatus = "Membuka browser untuk otentikasi Google...";
            StatusChanged?.Invoke();

            var drive = await CreateDriveServiceAsync(ct);
            if (drive == null)
            {
                IsSyncing = false;
                return false;
            }

            var aboutReq = drive.About.Get();
            aboutReq.Fields = "user(emailAddress,displayName)";
            var about = await aboutReq.ExecuteAsync(ct);

            var email = about.User?.EmailAddress ?? "Google User";
            _settingsService.CurrentSettings.GoogleDriveUserEmail = email;
            _settingsService.Save();

            _syncStatus = $"Tersambung ({email})";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal login Google: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (Directory.Exists(_authDataStorePath))
            {
                Directory.Delete(_authDataStorePath, true);
            }
        }
        catch { }

        _settingsService.CurrentSettings.GoogleDriveUserEmail = string.Empty;
        _settingsService.CurrentSettings.GoogleDriveLastSyncTime = null;
        _settingsService.Save();

        _syncStatus = "Sambungan diputuskan";
        StatusChanged?.Invoke();
        return Task.CompletedTask;
    }

    private async Task<string> GetOrCreateFolderAsync(DriveService service, CancellationToken ct)
    {
        var listReq = service.Files.List();
        listReq.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{FolderName}' and trashed = false";
        listReq.Fields = "files(id, name)";
        var result = await listReq.ExecuteAsync(ct);

        if (result.Files?.Count > 0)
        {
            return result.Files[0].Id;
        }

        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = FolderName,
            MimeType = "application/vnd.google-apps.folder"
        };

        var createReq = service.Files.Create(folderMetadata);
        createReq.Fields = "id";
        var folder = await createReq.ExecuteAsync(ct);
        return folder.Id;
    }

    private async Task<Google.Apis.Drive.v3.Data.File?> FindTodoFileAsync(DriveService service, string folderId, CancellationToken ct)
    {
        var listReq = service.Files.List();
        listReq.Q = $"'{folderId}' in parents and name = '{FileName}' and trashed = false";
        listReq.Fields = "files(id, name, modifiedTime)";
        var result = await listReq.ExecuteAsync(ct);
        return result.Files?.FirstOrDefault();
    }

    public async Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;

            var folderId = await GetOrCreateFolderAsync(service, ct);
            var file = await FindTodoFileAsync(service, folderId, ct);
            if (file == null) return null;

            using var stream = new MemoryStream();
            await service.Files.Get(file.Id).DownloadAsync(stream, ct);
            stream.Position = 0;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync(ct);
            return JsonSerializer.Deserialize<List<TodoItem>>(json);
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unduh dari Google Drive: {ex.Message}";
            StatusChanged?.Invoke();
            return null;
        }
    }

    public async Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default)
    {
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan ke Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return false;
            }

            var folderId = await GetOrCreateFolderAsync(service, ct);
            var existingFile = await FindTodoFileAsync(service, folderId, ct);

            var json = JsonSerializer.Serialize(todos, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            using var stream = new MemoryStream(bytes);

            if (existingFile != null)
            {
                var updateReq = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, stream, "application/json");
                await updateReq.UploadAsync(ct);
            }
            else
            {
                var metadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = FileName,
                    Parents = new List<string> { folderId }
                };
                var createReq = service.Files.Create(metadata, stream, "application/json");
                await createReq.UploadAsync(ct);
            }

            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Tersinkron ({DateTime.Now:HH:mm})";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unggah ke Google Drive: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return false;
        }
    }

    public async Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        lock (_syncLock)
        {
            if (_isSyncing) return null;
            _isSyncing = true;
        }

        try
        {
            _syncStatus = "Menyinkronkan dengan Google Drive...";
            StatusChanged?.Invoke();

            var remoteTodos = await DownloadTodosAsync(ct);

            // Merge local and remote
            var merged = MergeTodos(localTodos, remoteTodos ?? new List<TodoItem>());

            await UploadTodosAsync(merged, ct);

            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Sinkronisasi berhasil ({DateTime.Now:HH:mm})";
            TodosSynced?.Invoke(merged);
            return merged;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal sinkronisasi: {ex.Message}";
            return null;
        }
        finally
        {
            lock (_syncLock)
            {
                _isSyncing = false;
            }
            StatusChanged?.Invoke();
        }
    }

    public static List<TodoItem> MergeTodos(IEnumerable<TodoItem> local, IEnumerable<TodoItem> remote)
    {
        var dict = new Dictionary<string, TodoItem>();

        foreach (var item in remote)
        {
            dict[item.Id] = item;
        }

        foreach (var localItem in local)
        {
            if (dict.TryGetValue(localItem.Id, out var remoteItem))
            {
                // Prefer the completed state or later completed timestamp
                if (localItem.IsCompleted && !remoteItem.IsCompleted)
                {
                    dict[localItem.Id] = localItem;
                }
                else if (remoteItem.IsCompleted && !localItem.IsCompleted)
                {
                    // keep remote
                }
                else
                {
                    // If both same completion status, prefer whichever was updated/created
                    dict[localItem.Id] = localItem;
                }
            }
            else
            {
                dict[localItem.Id] = localItem;
            }
        }

        return dict.Values
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
    }
}
