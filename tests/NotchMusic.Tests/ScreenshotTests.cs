using System.Drawing;
using System.IO;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Screenshot;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

public class ScreenshotTests : IDisposable
{
    private readonly string _tempFolder;

    public ScreenshotTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "DRANOTCH_ScreenshotTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        catch { }
    }

    private class FakeMediaSessionService : IMediaSessionService
    {
        public event EventHandler<TrackInfo?>? TrackChanged;
        public event EventHandler<TimeSpan>? TimelineUpdated;
        public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

        public TrackInfo? CurrentTrack => null;
        public IReadOnlyList<MediaSessionInfo> AvailableSessions => new List<MediaSessionInfo>();
        public string? SelectedSessionId => null;

        public Task InitializeAsync() => Task.CompletedTask;
        public void SelectSession(string sessionId) { }
        public Task TogglePlayPauseAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public Task NextAsync() => Task.CompletedTask;
        public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
        public void Dispose() { }
        public event EventHandler? PlaybackStartedOnSpotify;
        public event EventHandler? PlaybackStartedOnYouTubeMusic;
        public Task PauseSessionByKeywordAsync(string keyword) => Task.CompletedTask;
    }

    private class FakeLyricsService : ILyricsService
    {
        public Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(new SongLyrics());
        public Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LrcLibSearchResult>());
        public SongLyrics LoadFromLocalFile(string filePath) => new SongLyrics();
    }

    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => Path.GetTempPath();
        public string GetLyricsCachePath() => Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    private class FakeScreenshotGoogleDriveSyncService : IGoogleDriveSyncService
    {
        public bool IsConfigured => true;
        public bool IsConnected => true;
        public bool IsSyncing => false;
        public string UserEmail => "user@gmail.com";
        public DateTime? LastSyncTime => DateTime.Now;
        public string SyncStatus => "Tersambung";

        public int SyncScreenshotsCallCount { get; private set; }

        public event Action? StatusChanged;
        public event Action<List<TodoItem>>? TodosSynced;
        public event Action<List<StickyNoteItem>>? StickyNotesSynced;
        public event Action<List<ScreenshotItem>>? ScreenshotsSynced;

        public Task<bool> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, IEnumerable<string>? deletedIds = null, CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(localTodos.ToList());
        public Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(new List<TodoItem>());
        public Task<List<TodoItem>?> CheckForRemoteChangesAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(null);
        public Task<bool> UploadStickyNotesAsync(IEnumerable<StickyNoteItem> notes, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<StickyNoteItem>?> DownloadStickyNotesAsync(CancellationToken ct = default) => Task.FromResult<List<StickyNoteItem>?>(new List<StickyNoteItem>());
        public Task<List<StickyNoteItem>?> CheckForRemoteStickyNotesChangesAsync(CancellationToken ct = default) => Task.FromResult<List<StickyNoteItem>?>(null);
        public Task<List<StickyNoteItem>?> SyncStickyNotesAsync(IEnumerable<StickyNoteItem> localNotes, IEnumerable<string>? deletedIds = null, CancellationToken ct = default) => Task.FromResult<List<StickyNoteItem>?>(localNotes.ToList());

        public Task<List<ScreenshotItem>?> SyncScreenshotsAsync(IEnumerable<ScreenshotItem> localScreenshots, IEnumerable<string>? deletedIds = null, string localFolder = "", CancellationToken ct = default)
        {
            SyncScreenshotsCallCount++;
            var list = localScreenshots.ToList();
            list.Add(new ScreenshotItem
            {
                Id = "cloud-snip-1",
                FileName = "Snip_Cloud.png",
                FilePath = Path.Combine(localFolder, "Snip_Cloud.png"),
                Title = "Cloud Snip",
                CaptureMode = "Region",
                CreatedAt = DateTime.Now
            });
            return Task.FromResult<List<ScreenshotItem>?>(list);
        }

        public Task<bool> UploadScreenshotsMetadataAsync(IEnumerable<ScreenshotItem> screenshots, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<ScreenshotItem>?> DownloadScreenshotsMetadataAsync(CancellationToken ct = default) => Task.FromResult<List<ScreenshotItem>?>(new List<ScreenshotItem>());
        public Task<List<ScreenshotItem>?> CheckForRemoteScreenshotsChangesAsync(string localFolder = "", CancellationToken ct = default) => Task.FromResult<List<ScreenshotItem>?>(null);
        public bool ImportCredentialsFile(string filePath) => true;
    }

    [Fact]
    public void SaveScreenshot_CreatesImageFileAndMetadata()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(160, 90);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Blue);
        }

        var item = service.SaveScreenshot(bmp, "Region", "Test Region Snip");

        Assert.NotNull(item);
        Assert.True(File.Exists(item.FilePath));
        Assert.Equal(160, item.Width);
        Assert.Equal(90, item.Height);
        Assert.Equal("Region", item.CaptureMode);
        Assert.Equal("Test Region Snip", item.Title);

        var list = service.LoadScreenshots();
        Assert.Single(list);
        Assert.Equal(item.Id, list[0].Id);
    }

    [Fact]
    public void MultiScreenshots_CanStoreManyCapturesAccurately()
    {
        var service = new ScreenshotService(_tempFolder);

        for (int i = 1; i <= 5; i++)
        {
            using var bmp = new Bitmap(50 * i, 40 * i);
            service.SaveScreenshot(bmp, i % 2 == 0 ? "FullScreen" : "Region", $"Snip #{i}");
        }

        var list = service.LoadScreenshots();
        Assert.Equal(5, list.Count);
        Assert.Equal("Snip #5", list[0].Title); // Newest first
    }

    [Fact]
    public void DeleteScreenshot_RemovesFileAndUpdatesMetadata()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp1 = new Bitmap(100, 100);
        using var bmp2 = new Bitmap(200, 200);

        var item1 = service.SaveScreenshot(bmp1, "Region", "Snip 1");
        var item2 = service.SaveScreenshot(bmp2, "Region", "Snip 2");

        Assert.Equal(2, service.LoadScreenshots().Count);
        Assert.True(File.Exists(item1.FilePath));

        bool deleted = service.DeleteScreenshot(item1.Id);
        Assert.True(deleted);

        var remaining = service.LoadScreenshots();
        Assert.Single(remaining);
        Assert.Equal(item2.Id, remaining[0].Id);
        Assert.False(File.Exists(item1.FilePath));
    }

    [Fact]
    public void ClearAllScreenshots_RemovesAllFiles()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(100, 100);
        var item1 = service.SaveScreenshot(bmp, "Region", "Snip 1");
        var item2 = service.SaveScreenshot(bmp, "Region", "Snip 2");

        service.ClearAllScreenshots();

        Assert.Empty(service.LoadScreenshots());
        Assert.False(File.Exists(item1.FilePath));
        Assert.False(File.Exists(item2.FilePath));
    }

    [Fact]
    public void UpdateScreenshot_PersistsTitleAndNotes()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(100, 100);
        var item = service.SaveScreenshot(bmp, "Region", "Original Title");

        item.Title = "Renamed Snip";
        item.Note = "Penting untuk referensi coding";
        item.IsPinned = true;
        service.UpdateScreenshot(item);

        var reloadedService = new ScreenshotService(_tempFolder);
        var list = reloadedService.LoadScreenshots();
        Assert.Single(list);
        Assert.Equal("Renamed Snip", list[0].Title);
        Assert.Equal("Penting untuk referensi coding", list[0].Note);
        Assert.True(list[0].IsPinned);
    }

    [Fact]
    public void ExportAll_CopiesAllImagesToTargetFolder()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(80, 80);
        service.SaveScreenshot(bmp, "Region", "Export 1");
        service.SaveScreenshot(bmp, "FullScreen", "Export 2");

        var exportTarget = Path.Combine(_tempFolder, "ExportedFiles");
        int count = service.ExportAll(exportTarget);

        Assert.Equal(2, count);
        Assert.True(Directory.Exists(exportTarget));
        Assert.Equal(2, Directory.GetFiles(exportTarget, "*.png").Length);
    }

    [Fact]
    public void ScreenshotItemViewModel_FormatsValuesCorrectly()
    {
        var item = new ScreenshotItem
        {
            Title = "Sample Snip",
            Width = 1920,
            Height = 1080,
            FileSize = 512 * 1024,
            CaptureMode = "FullScreen",
            CreatedAt = DateTime.Now
        };

        var vm = new ScreenshotItemViewModel(item);
        Assert.Equal("1920 × 1080 px", vm.FormattedResolution);
        Assert.Equal("512.0 KB", vm.FormattedFileSize);
        Assert.Equal("Layar Penuh", vm.ModeLabel);
        Assert.StartsWith("Hari ini", vm.FormattedDate);
    }

    [Fact]
    public void MainViewModel_ScreenshotPropertiesAndCommandsIntegration()
    {
        var settingsService = new FakeSettingsService();
        var screenshotService = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(100, 100);
        screenshotService.SaveScreenshot(bmp, "Region", "Notch Snip Test");

        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService,
            screenshotService: screenshotService);

        Assert.True(vm.EnableScreenshotTool);
        Assert.Equal(1, vm.ScreenshotCount);
        Assert.True(vm.HasScreenshots);
        Assert.Contains("1 Gambar Tersimpan", vm.ScreenshotNotchTooltip);

        bool openRequested = false;
        vm.RequestOpenScreenshotHub += () => openRequested = true;
        vm.OpenScreenshotHubCommand.Execute(null);
        Assert.True(openRequested);
        Assert.True(vm.IsScreenshotHubOpen);

        bool snipRequested = false;
        SnipMode? capturedMode = null;
        vm.RequestStartSnipping += mode =>
        {
            snipRequested = true;
            capturedMode = mode;
        };
        vm.StartSnippingCommand.Execute(null);
        Assert.True(snipRequested);
        Assert.Equal(SnipMode.Rectangular, capturedMode);

        // Test CaptureActiveWindow opens window selection mode
        vm.CaptureActiveWindowCommand.Execute(null);
        Assert.Equal(SnipMode.Window, capturedMode);
    }

    [Fact]
    public void ScreenshotService_TracksDeletedIdsOnDeleteAndClear()
    {
        var service = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(50, 50);
        var item1 = service.SaveScreenshot(bmp, "Region", "Snip 1");
        var item2 = service.SaveScreenshot(bmp, "Region", "Snip 2");

        Assert.Empty(service.GetDeletedIds());

        // Delete item1 -> ID should be tracked in deletedIds
        service.DeleteScreenshot(item1.Id);
        var deleted = service.GetDeletedIds();
        Assert.Contains(item1.Id, deleted);
        Assert.DoesNotContain(item2.Id, deleted);

        // Verify persistence: create a new service instance pointing to the same folder
        var newService = new ScreenshotService(_tempFolder);
        var reloadedDeleted = newService.GetDeletedIds();
        Assert.Contains(item1.Id, reloadedDeleted);

        // Clear deleted ids
        newService.ClearDeletedIds(new[] { item1.Id });
        Assert.Empty(newService.GetDeletedIds());

        // ClearAll -> all remaining IDs tracked
        newService.ClearAllScreenshots();
        Assert.Contains(item2.Id, newService.GetDeletedIds());
    }

    [Fact]
    public void GoogleDriveSyncService_MergeScreenshots_RespectsDeletedIdsAndCombinesCorrectly()
    {
        var local = new List<ScreenshotItem>
        {
            new() { Id = "s1", Title = "Local Snip 1", FileName = "s1.png", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = "s2", Title = "Local Snip 2 (Deleted)", FileName = "s2.png", CreatedAt = DateTime.UtcNow.AddMinutes(-4) }
        };

        var remote = new List<ScreenshotItem>
        {
            new() { Id = "s2", Title = "Remote Snip 2", FileName = "s2.png", CreatedAt = DateTime.UtcNow.AddMinutes(-4) },
            new() { Id = "s3", Title = "Remote Snip 3", FileName = "s3.png", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
        };

        var deletedIds = new[] { "s2" };

        var merged = GoogleDriveSyncService.MergeScreenshots(local, remote, deletedIds);

        Assert.Equal(2, merged.Count);
        Assert.DoesNotContain(merged, s => s.Id == "s2"); // s2 must be filtered out because it is deleted
        Assert.Contains(merged, s => s.Id == "s1");
        Assert.Contains(merged, s => s.Id == "s3");

        // Verify sorted descending by CreatedAt (s3 is newer than s1)
        Assert.Equal("s3", merged[0].Id);
        Assert.Equal("s1", merged[1].Id);
    }

    [Fact]
    public async Task MainViewModel_GoogleDriveScreenshotSync_SyncsScreenshots()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.GoogleDriveScreenshotSync = true;

        var screenshotService = new ScreenshotService(_tempFolder);
        using var bmp = new Bitmap(60, 60);
        screenshotService.SaveScreenshot(bmp, "Region", "Local Snip");

        var syncService = new FakeScreenshotGoogleDriveSyncService();

        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService,
            screenshotService: screenshotService,
            googleDriveSyncService: syncService);

        Assert.True(vm.GoogleDriveScreenshotSync);
        Assert.Equal(1, vm.ScreenshotCount);

        // Execute sync command
        await vm.SyncScreenshotsWithGoogleDriveAsync();

        Assert.Equal(1, syncService.SyncScreenshotsCallCount);
        Assert.Equal(2, vm.ScreenshotCount); // 1 local + 1 cloud snip merged
        Assert.Contains(screenshotService.LoadScreenshots(), s => s.Id == "cloud-snip-1");
    }
}
