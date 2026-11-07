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
    private const string StickyNotesFileName = "stickynotes.json";
    private const string ScreenshotsFolderName = "Screenshots";
    private const string ScreenshotsMetadataFileName = "screenshots.json";
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

    // Kredensial Bawaan Aplikasi DRANOTCH
    private static readonly byte[] EncId = new byte[] { 105, 107, 107, 109, 108, 109, 109, 110, 106, 111, 108, 107, 119, 55, 63, 106, 57, 43, 44, 52, 46, 40, 49, 40, 104, 106, 47, 106, 104, 43, 56, 42, 55, 60, 98, 106, 60, 56, 105, 59, 44, 41, 44, 98, 40, 116, 59, 42, 42, 41, 116, 61, 53, 53, 61, 54, 63, 47, 41, 63, 40, 57, 53, 52, 46, 63, 52, 46, 116, 57, 53, 55 };
    private static readonly byte[] EncSec = new byte[] { 29, 21, 25, 9, 10, 2, 119, 52, 48, 2, 56, 57, 34, 104, 56, 111, 12, 10, 99, 16, 9, 40, 30, 22, 21, 23, 9, 28, 14, 119, 31, 45, 19, 16, 13 };

    public static readonly string DefaultClientId = DecodeCred(EncId);
    public static readonly string DefaultClientSecret = DecodeCred(EncSec);

    private static string DecodeCred(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;
        var b = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) b[i] = (byte)(data[i] ^ 0x5A);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    private readonly ISettingsService _settingsService;
    private readonly string _authDataStorePath;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private bool _isSyncing;
    private string _syncStatus = "Belum terhubung";
    private DateTimeOffset? _lastKnownRemoteModifiedTime;
    private DateTimeOffset? _lastKnownRemoteStickyNotesModifiedTime;
    private DateTimeOffset? _lastKnownRemoteScreenshotsModifiedTime;

    public string EffectiveClientId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.GoogleDriveClientId))
                return _settingsService.CurrentSettings.GoogleDriveClientId.Trim();

            var envId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(envId))
                return envId.Trim();

            return DefaultClientId;
        }
    }

    public string EffectiveClientSecret
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.GoogleDriveClientSecret))
                return _settingsService.CurrentSettings.GoogleDriveClientSecret.Trim();

            var envSecret = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(envSecret))
                return envSecret.Trim();

            return DefaultClientSecret;
        }
    }

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
    public event Action<List<StickyNoteItem>>? StickyNotesSynced;
    public event Action<List<ScreenshotItem>>? ScreenshotsSynced;

    public GoogleDriveSyncService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _authDataStorePath = Path.Combine(_settingsService.GetAppDataPath(), "GoogleAuthDataStore");

        // Auto-discover credentials.json if not configured
        if (!IsConfigured)
        {
            TryAutoDiscoverCredentials();
        }

        if (IsConnected)
        {
            _syncStatus = $"Tersambung ({UserEmail})";
        }
        else if (IsConfigured)
        {
            _syncStatus = "Siap login";
        }
        else
        {
            _syncStatus = "Kredensial belum disetel (impor credentials.json)";
        }
    }

    private void TryAutoDiscoverCredentials()
    {
        try
        {
            var searchPaths = new[]
            {
                Path.Combine(_settingsService.GetAppDataPath(), "credentials.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "credentials.json")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path) && ImportCredentialsFile(path))
                {
                    break;
                }
            }
        }
        catch
        {
            // Silently ignore auto-discovery errors
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

    private async Task<List<TodoItem>?> DownloadTodosInternalAsync(DriveService service, CancellationToken ct)
    {
        var folderId = await GetOrCreateFolderAsync(service, ct);
        var file = await FindTodoFileAsync(service, folderId, ct);
        if (file == null) return null;

        using var stream = new MemoryStream();
        await service.Files.Get(file.Id).DownloadAsync(stream, ct);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        _lastKnownRemoteModifiedTime = file.ModifiedTimeDateTimeOffset;
        return JsonSerializer.Deserialize<List<TodoItem>>(json);
    }

    public async Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;
            return await DownloadTodosInternalAsync(service, ct);
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unduh dari Google Drive: {ex.Message}";
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<TodoItem>?> CheckForRemoteChangesAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        if (!await _syncLock.WaitAsync(0, ct)) return null;

        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;

            var folderId = await GetOrCreateFolderAsync(service, ct);
            var file = await FindTodoFileAsync(service, folderId, ct);
            if (file == null) return null;

            var remoteModTime = file.ModifiedTimeDateTimeOffset;
            if (remoteModTime.HasValue && _lastKnownRemoteModifiedTime.HasValue && remoteModTime <= _lastKnownRemoteModifiedTime)
            {
                // File di Google Drive belum berubah sejak terakhir diperiksa
                return null;
            }

            // File di Google Drive telah berubah oleh perangkat lain! Unduh otomatis
            var remoteList = await DownloadTodosInternalAsync(service, ct);

            _lastKnownRemoteModifiedTime = remoteModTime;
            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Tersinkron otomatis ({DateTime.Now:HH:mm})";
            StatusChanged?.Invoke();

            if (remoteList != null)
            {
                TodosSynced?.Invoke(remoteList);
            }

            return remoteList;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckForRemoteChanges error: {ex.Message}");
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<bool> UploadTodosInternalAsync(DriveService service, IEnumerable<TodoItem> todos, CancellationToken ct)
    {
        var folderId = await GetOrCreateFolderAsync(service, ct);
        var existingFile = await FindTodoFileAsync(service, folderId, ct);

        var json = JsonSerializer.Serialize(todos, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);

        if (existingFile != null)
        {
            var updateReq = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, stream, "application/json");
            updateReq.Fields = "id, modifiedTime";
            var progress = await updateReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal mengunggah ke Google Drive.");
            }
            _lastKnownRemoteModifiedTime = updateReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }
        else
        {
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = FileName,
                Parents = new List<string> { folderId }
            };
            var createReq = service.Files.Create(metadata, stream, "application/json");
            createReq.Fields = "id, modifiedTime";
            var progress = await createReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal membuat file di Google Drive.");
            }
            _lastKnownRemoteModifiedTime = createReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }

        return true;
    }

    public async Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
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

            bool success = await UploadTodosInternalAsync(service, todos, ct);
            if (success)
            {
                _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
                _settingsService.Save();
                _syncStatus = $"Tersinkron ({DateTime.Now:HH:mm})";
            }
            IsSyncing = false;
            StatusChanged?.Invoke();
            return success;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unggah ke Google Drive: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return false;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, IEnumerable<string>? deletedIds = null, CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        await _syncLock.WaitAsync(ct);
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan dengan Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return null;
            }

            var remoteTodos = await DownloadTodosInternalAsync(service, ct);

            // Merge local and remote
            var merged = MergeTodos(localTodos, remoteTodos ?? new List<TodoItem>(), deletedIds);

            await UploadTodosInternalAsync(service, merged, ct);

            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Sinkronisasi berhasil ({DateTime.Now:HH:mm})";
            IsSyncing = false;
            StatusChanged?.Invoke();

            TodosSynced?.Invoke(merged);
            return merged;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal sinkronisasi: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public static List<TodoItem> MergeTodos(IEnumerable<TodoItem> local, IEnumerable<TodoItem> remote, IEnumerable<string>? deletedIds = null)
    {
        var deletedSet = deletedIds != null ? new HashSet<string>(deletedIds) : new HashSet<string>();
        var dict = new Dictionary<string, TodoItem>();

        foreach (var item in remote)
        {
            if (deletedSet.Contains(item.Id))
            {
                continue;
            }
            dict[item.Id] = item;
        }

        foreach (var localItem in local)
        {
            if (deletedSet.Contains(localItem.Id))
            {
                continue;
            }

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

    private async Task<Google.Apis.Drive.v3.Data.File?> FindStickyNotesFileAsync(DriveService service, string folderId, CancellationToken ct)
    {
        var listReq = service.Files.List();
        listReq.Q = $"'{folderId}' in parents and name = '{StickyNotesFileName}' and trashed = false";
        listReq.Fields = "files(id, name, modifiedTime)";
        var result = await listReq.ExecuteAsync(ct);
        return result.Files?.FirstOrDefault();
    }

    private async Task<List<StickyNoteItem>?> DownloadStickyNotesInternalAsync(DriveService service, CancellationToken ct)
    {
        var folderId = await GetOrCreateFolderAsync(service, ct);
        var file = await FindStickyNotesFileAsync(service, folderId, ct);
        if (file == null) return null;

        using var stream = new MemoryStream();
        await service.Files.Get(file.Id).DownloadAsync(stream, ct);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        _lastKnownRemoteStickyNotesModifiedTime = file.ModifiedTimeDateTimeOffset;
        return JsonSerializer.Deserialize<List<StickyNoteItem>>(json);
    }

    public async Task<List<StickyNoteItem>?> DownloadStickyNotesAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;
            return await DownloadStickyNotesInternalAsync(service, ct);
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unduh catatan dari Google Drive: {ex.Message}";
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<StickyNoteItem>?> CheckForRemoteStickyNotesChangesAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        if (!await _syncLock.WaitAsync(0, ct)) return null;

        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;

            var folderId = await GetOrCreateFolderAsync(service, ct);
            var file = await FindStickyNotesFileAsync(service, folderId, ct);
            if (file == null) return null;

            var remoteModTime = file.ModifiedTimeDateTimeOffset;
            if (remoteModTime.HasValue && _lastKnownRemoteStickyNotesModifiedTime.HasValue && remoteModTime <= _lastKnownRemoteStickyNotesModifiedTime)
            {
                return null;
            }

            var remoteList = await DownloadStickyNotesInternalAsync(service, ct);

            _lastKnownRemoteStickyNotesModifiedTime = remoteModTime;
            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Catatan tersinkron otomatis ({DateTime.Now:HH:mm})";
            StatusChanged?.Invoke();

            if (remoteList != null)
            {
                StickyNotesSynced?.Invoke(remoteList);
            }

            return remoteList;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckForRemoteStickyNotesChanges error: {ex.Message}");
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<bool> UploadStickyNotesInternalAsync(DriveService service, IEnumerable<StickyNoteItem> notes, CancellationToken ct)
    {
        var folderId = await GetOrCreateFolderAsync(service, ct);
        var existingFile = await FindStickyNotesFileAsync(service, folderId, ct);

        var json = JsonSerializer.Serialize(notes, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);

        if (existingFile != null)
        {
            var updateReq = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, stream, "application/json");
            updateReq.Fields = "id, modifiedTime";
            var progress = await updateReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal mengunggah catatan ke Google Drive.");
            }
            _lastKnownRemoteStickyNotesModifiedTime = updateReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }
        else
        {
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = StickyNotesFileName,
                Parents = new List<string> { folderId }
            };
            var createReq = service.Files.Create(metadata, stream, "application/json");
            createReq.Fields = "id, modifiedTime";
            var progress = await createReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal membuat file catatan di Google Drive.");
            }
            _lastKnownRemoteStickyNotesModifiedTime = createReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }

        return true;
    }

    public async Task<bool> UploadStickyNotesAsync(IEnumerable<StickyNoteItem> notes, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan catatan ke Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return false;
            }

            bool success = await UploadStickyNotesInternalAsync(service, notes, ct);
            if (success)
            {
                _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
                _settingsService.Save();
                _syncStatus = $"Catatan tersinkron ({DateTime.Now:HH:mm})";
            }
            IsSyncing = false;
            StatusChanged?.Invoke();
            return success;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unggah catatan ke Google Drive: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return false;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<StickyNoteItem>?> SyncStickyNotesAsync(IEnumerable<StickyNoteItem> localNotes, IEnumerable<string>? deletedIds = null, CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        await _syncLock.WaitAsync(ct);
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan catatan dengan Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return null;
            }

            var remoteNotes = await DownloadStickyNotesInternalAsync(service, ct);

            var merged = MergeStickyNotes(localNotes, remoteNotes ?? new List<StickyNoteItem>(), deletedIds);

            await UploadStickyNotesInternalAsync(service, merged, ct);

            _settingsService.CurrentSettings.GoogleDriveLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Sinkronisasi catatan berhasil ({DateTime.Now:HH:mm})";
            IsSyncing = false;
            StatusChanged?.Invoke();

            StickyNotesSynced?.Invoke(merged);
            return merged;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal sinkronisasi catatan: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public static List<StickyNoteItem> MergeStickyNotes(IEnumerable<StickyNoteItem> local, IEnumerable<StickyNoteItem> remote, IEnumerable<string>? deletedIds = null)
    {
        var deletedSet = deletedIds != null ? new HashSet<string>(deletedIds) : new HashSet<string>();
        var dict = new Dictionary<string, StickyNoteItem>();

        foreach (var item in remote)
        {
            if (deletedSet.Contains(item.Id))
            {
                continue;
            }
            dict[item.Id] = item;
        }

        foreach (var localItem in local)
        {
            if (deletedSet.Contains(localItem.Id))
            {
                continue;
            }

            if (dict.TryGetValue(localItem.Id, out var remoteItem))
            {
                // The item with later UpdatedAt wins
                if (remoteItem.UpdatedAt > localItem.UpdatedAt)
                {
                    dict[localItem.Id] = remoteItem;
                }
                else
                {
                    dict[localItem.Id] = localItem;
                }
            }
            else
            {
                dict[localItem.Id] = localItem;
            }
        }

        return dict.Values
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .ToList();
    }

    #region Screenshot 2-Way Sync
    private async Task<string> GetOrCreateScreenshotsFolderAsync(DriveService service, CancellationToken ct)
    {
        var parentFolderId = await GetOrCreateFolderAsync(service, ct);

        var listReq = service.Files.List();
        listReq.Q = $"'{parentFolderId}' in parents and name = '{ScreenshotsFolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        listReq.Fields = "files(id, name)";
        var result = await listReq.ExecuteAsync(ct);

        var folder = result.Files?.FirstOrDefault();
        if (folder != null)
        {
            return folder.Id;
        }

        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = ScreenshotsFolderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { parentFolderId }
        };

        var createReq = service.Files.Create(folderMetadata);
        createReq.Fields = "id";
        var created = await createReq.ExecuteAsync(ct);
        return created.Id;
    }

    private async Task<Google.Apis.Drive.v3.Data.File?> FindScreenshotsMetadataFileAsync(DriveService service, string folderId, CancellationToken ct)
    {
        var listReq = service.Files.List();
        listReq.Q = $"'{folderId}' in parents and name = '{ScreenshotsMetadataFileName}' and trashed = false";
        listReq.Fields = "files(id, name, modifiedTime)";
        var result = await listReq.ExecuteAsync(ct);
        return result.Files?.FirstOrDefault();
    }

    private async Task<List<Google.Apis.Drive.v3.Data.File>> ListScreenshotsFolderFilesAsync(DriveService service, string folderId, CancellationToken ct)
    {
        var allFiles = new List<Google.Apis.Drive.v3.Data.File>();
        string? pageToken = null;
        do
        {
            var listReq = service.Files.List();
            listReq.Q = $"'{folderId}' in parents and trashed = false";
            listReq.Fields = "nextPageToken, files(id, name, modifiedTime, size)";
            listReq.PageToken = pageToken;
            listReq.PageSize = 100;

            var result = await listReq.ExecuteAsync(ct);
            if (result.Files != null && result.Files.Count > 0)
            {
                allFiles.AddRange(result.Files);
            }
            pageToken = result.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return allFiles;
    }

    private async Task<List<ScreenshotItem>?> DownloadScreenshotsMetadataInternalAsync(DriveService service, CancellationToken ct)
    {
        var folderId = await GetOrCreateScreenshotsFolderAsync(service, ct);
        var file = await FindScreenshotsMetadataFileAsync(service, folderId, ct);
        if (file == null) return null;

        using var stream = new MemoryStream();
        await service.Files.Get(file.Id).DownloadAsync(stream, ct);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        _lastKnownRemoteScreenshotsModifiedTime = file.ModifiedTimeDateTimeOffset;
        return JsonSerializer.Deserialize<List<ScreenshotItem>>(json);
    }

    private async Task<bool> UploadScreenshotsMetadataInternalAsync(DriveService service, IEnumerable<ScreenshotItem> screenshots, CancellationToken ct)
    {
        var folderId = await GetOrCreateScreenshotsFolderAsync(service, ct);
        var existingFile = await FindScreenshotsMetadataFileAsync(service, folderId, ct);

        var json = JsonSerializer.Serialize(screenshots, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);

        if (existingFile != null)
        {
            var updateReq = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, stream, "application/json");
            updateReq.Fields = "id, modifiedTime";
            var progress = await updateReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal mengunggah metadata screenshot ke Google Drive.");
            }
            _lastKnownRemoteScreenshotsModifiedTime = updateReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }
        else
        {
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = ScreenshotsMetadataFileName,
                Parents = new List<string> { folderId }
            };
            var createReq = service.Files.Create(metadata, stream, "application/json");
            createReq.Fields = "id, modifiedTime";
            var progress = await createReq.UploadAsync(ct);
            if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw progress.Exception ?? new Exception("Gagal membuat metadata screenshot di Google Drive.");
            }
            _lastKnownRemoteScreenshotsModifiedTime = createReq.ResponseBody?.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        }

        return true;
    }

    private async Task UploadSingleImageFileAsync(DriveService service, string folderId, string localFilePath, string fileName, CancellationToken ct)
    {
        if (!File.Exists(localFilePath)) return;

        using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = new List<string> { folderId }
        };

        var createReq = service.Files.Create(metadata, fileStream, "image/png");
        createReq.Fields = "id, name";
        var progress = await createReq.UploadAsync(ct);
        if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
        {
            throw progress.Exception ?? new Exception($"Gagal mengunggah gambar {fileName} ke Google Drive.");
        }
    }

    private async Task DownloadSingleImageFileAsync(DriveService service, string fileId, string targetLocalPath, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(targetLocalPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempTarget = targetLocalPath + ".tmp";
        using (var stream = new FileStream(tempTarget, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await service.Files.Get(fileId).DownloadAsync(stream, ct);
        }

        if (File.Exists(targetLocalPath))
        {
            File.Delete(targetLocalPath);
        }
        File.Move(tempTarget, targetLocalPath);
    }

    public static List<ScreenshotItem> MergeScreenshots(IEnumerable<ScreenshotItem> local, IEnumerable<ScreenshotItem> remote, IEnumerable<string>? deletedIds = null)
    {
        var deletedSet = deletedIds != null ? new HashSet<string>(deletedIds) : new HashSet<string>();
        var dict = new Dictionary<string, ScreenshotItem>();

        foreach (var item in remote)
        {
            if (deletedSet.Contains(item.Id))
            {
                continue;
            }
            dict[item.Id] = item;
        }

        foreach (var localItem in local)
        {
            if (deletedSet.Contains(localItem.Id))
            {
                continue;
            }

            if (dict.TryGetValue(localItem.Id, out var remoteItem))
            {
                // Prefer local file path if valid
                if (File.Exists(localItem.FilePath))
                {
                    remoteItem.FilePath = localItem.FilePath;
                }
                // Prefer local title/note if updated
                dict[localItem.Id] = localItem;
            }
            else
            {
                dict[localItem.Id] = localItem;
            }
        }

        return dict.Values
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public async Task<List<ScreenshotItem>?> SyncScreenshotsAsync(IEnumerable<ScreenshotItem> localScreenshots, IEnumerable<string>? deletedIds = null, string localFolder = "", CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        await _syncLock.WaitAsync(ct);
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan tangkapan layar dengan Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return null;
            }

            var screenshotsFolderId = await GetOrCreateScreenshotsFolderAsync(service, ct);
            var driveFiles = await ListScreenshotsFolderFilesAsync(service, screenshotsFolderId, ct);

            // Clean up deleted items from remote Drive
            var deletedSet = deletedIds != null ? new HashSet<string>(deletedIds) : new HashSet<string>();
            if (deletedSet.Count > 0)
            {
                foreach (var file in driveFiles.ToList())
                {
                    // If fileName starts with an ID or matches deleted items
                    if (deletedSet.Any(id => file.Name.Contains(id, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            await service.Files.Delete(file.Id).ExecuteAsync(ct);
                            driveFiles.Remove(file);
                        }
                        catch { }
                    }
                }
            }

            var remoteScreenshots = await DownloadScreenshotsMetadataInternalAsync(service, ct);
            var merged = MergeScreenshots(localScreenshots, remoteScreenshots ?? new List<ScreenshotItem>(), deletedIds);

            if (string.IsNullOrWhiteSpace(localFolder))
            {
                localFolder = Path.Combine(_settingsService.GetAppDataPath(), "Screenshots");
            }
            Directory.CreateDirectory(localFolder);

            // Synchronize individual image files
            var remoteFilesMap = driveFiles.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var item in merged)
            {
                var expectedLocalPath = Path.Combine(localFolder, item.FileName);
                bool localExists = File.Exists(item.FilePath) || File.Exists(expectedLocalPath);

                if (localExists)
                {
                    var sourcePath = File.Exists(item.FilePath) ? item.FilePath : expectedLocalPath;
                    item.FilePath = sourcePath;

                    // If not on Drive, upload
                    if (!remoteFilesMap.ContainsKey(item.FileName))
                    {
                        try
                        {
                            await UploadSingleImageFileAsync(service, screenshotsFolderId, sourcePath, item.FileName, ct);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Upload screenshot {item.FileName} error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Not found locally: download from Drive if present
                    if (remoteFilesMap.TryGetValue(item.FileName, out var driveFile))
                    {
                        try
                        {
                            await DownloadSingleImageFileAsync(service, driveFile.Id, expectedLocalPath, ct);
                            item.FilePath = expectedLocalPath;
                            var fi = new FileInfo(expectedLocalPath);
                            if (fi.Exists)
                            {
                                item.FileSize = fi.Length;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Download screenshot {item.FileName} error: {ex.Message}");
                        }
                    }
                }
            }

            // Save updated metadata to Google Drive
            await UploadScreenshotsMetadataInternalAsync(service, merged, ct);

            _settingsService.CurrentSettings.GoogleDriveScreenshotLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Screenshot tersinkron ({DateTime.Now:HH:mm})";
            IsSyncing = false;
            StatusChanged?.Invoke();

            ScreenshotsSynced?.Invoke(merged);
            return merged;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal sinkronisasi screenshot: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> UploadScreenshotsMetadataAsync(IEnumerable<ScreenshotItem> screenshots, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            IsSyncing = true;
            _syncStatus = "Menyinkronkan metadata screenshot ke Google Drive...";
            StatusChanged?.Invoke();

            var service = await CreateDriveServiceAsync(ct);
            if (service == null)
            {
                IsSyncing = false;
                return false;
            }

            bool success = await UploadScreenshotsMetadataInternalAsync(service, screenshots, ct);
            if (success)
            {
                _settingsService.CurrentSettings.GoogleDriveScreenshotLastSyncTime = DateTime.Now;
                _settingsService.Save();
                _syncStatus = $"Screenshot tersinkron ({DateTime.Now:HH:mm})";
            }
            IsSyncing = false;
            StatusChanged?.Invoke();
            return success;
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unggah screenshot ke Google Drive: {ex.Message}";
            IsSyncing = false;
            StatusChanged?.Invoke();
            return false;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<ScreenshotItem>?> DownloadScreenshotsMetadataAsync(CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;
            return await DownloadScreenshotsMetadataInternalAsync(service, ct);
        }
        catch (Exception ex)
        {
            _syncStatus = $"Gagal unduh screenshot dari Google Drive: {ex.Message}";
            StatusChanged?.Invoke();
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<List<ScreenshotItem>?> CheckForRemoteScreenshotsChangesAsync(string localFolder = "", CancellationToken ct = default)
    {
        if (!IsConnected) return null;
        if (!await _syncLock.WaitAsync(0, ct)) return null;

        try
        {
            var service = await CreateDriveServiceAsync(ct);
            if (service == null) return null;

            var folderId = await GetOrCreateScreenshotsFolderAsync(service, ct);
            var file = await FindScreenshotsMetadataFileAsync(service, folderId, ct);
            if (file == null) return null;

            var remoteModTime = file.ModifiedTimeDateTimeOffset;
            if (remoteModTime.HasValue && _lastKnownRemoteScreenshotsModifiedTime.HasValue && remoteModTime <= _lastKnownRemoteScreenshotsModifiedTime)
            {
                return null;
            }

            var remoteList = await DownloadScreenshotsMetadataInternalAsync(service, ct);
            _lastKnownRemoteScreenshotsModifiedTime = remoteModTime;
            _settingsService.CurrentSettings.GoogleDriveScreenshotLastSyncTime = DateTime.Now;
            _settingsService.Save();

            _syncStatus = $"Screenshot tersinkron otomatis ({DateTime.Now:HH:mm})";
            StatusChanged?.Invoke();

            if (remoteList != null)
            {
                ScreenshotsSynced?.Invoke(remoteList);
            }

            return remoteList;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckForRemoteScreenshotsChanges error: {ex.Message}");
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }
    #endregion
}
