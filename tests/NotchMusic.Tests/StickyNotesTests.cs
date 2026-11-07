using System.IO;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.Services.StickyNotes;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

public class StickyNotesTests
{
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

    private class FakeGoogleDriveSyncService : IGoogleDriveSyncService
    {
        public bool IsConfigured => true;
        public bool IsConnected => true;
        public bool IsSyncing => false;
        public string UserEmail => "test@gmail.com";
        public DateTime? LastSyncTime => DateTime.Now;
        public string SyncStatus => "Tersambung";

        public event Action? StatusChanged;
        public event Action<List<TodoItem>>? TodosSynced;
        public event Action<List<StickyNoteItem>>? StickyNotesSynced;
        public event Action<List<ScreenshotItem>>? ScreenshotsSynced;

        public int UploadStickyNotesCallCount { get; private set; }
        public int SyncStickyNotesCallCount { get; private set; }

        public Task<bool> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, IEnumerable<string>? deletedIds = null, CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(localTodos.ToList());
        public Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(new List<TodoItem>());
        public Task<List<TodoItem>?> CheckForRemoteChangesAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(null);

        public Task<bool> UploadStickyNotesAsync(IEnumerable<StickyNoteItem> notes, CancellationToken ct = default)
        {
            UploadStickyNotesCallCount++;
            return Task.FromResult(true);
        }

        public Task<List<StickyNoteItem>?> DownloadStickyNotesAsync(CancellationToken ct = default) => Task.FromResult<List<StickyNoteItem>?>(new List<StickyNoteItem>());
        public Task<List<StickyNoteItem>?> CheckForRemoteStickyNotesChangesAsync(CancellationToken ct = default) => Task.FromResult<List<StickyNoteItem>?>(null);

        public Task<List<StickyNoteItem>?> SyncStickyNotesAsync(IEnumerable<StickyNoteItem> localNotes, IEnumerable<string>? deletedIds = null, CancellationToken ct = default)
        {
            SyncStickyNotesCallCount++;
            var list = localNotes.ToList();
            list.Add(new StickyNoteItem
            {
                Id = "cloud-note-1",
                Title = "Catatan dari Google Drive",
                Content = "Ini catatan awan",
                Color = "Blue",
                UpdatedAt = DateTime.Now
            });
            return Task.FromResult<List<StickyNoteItem>?>(list);
        }

        public Task<List<ScreenshotItem>?> SyncScreenshotsAsync(IEnumerable<ScreenshotItem> localScreenshots, IEnumerable<string>? deletedIds = null, string localFolder = "", CancellationToken ct = default) => Task.FromResult<List<ScreenshotItem>?>(localScreenshots.ToList());
        public Task<bool> UploadScreenshotsMetadataAsync(IEnumerable<ScreenshotItem> screenshots, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<ScreenshotItem>?> DownloadScreenshotsMetadataAsync(CancellationToken ct = default) => Task.FromResult<List<ScreenshotItem>?>(new List<ScreenshotItem>());
        public Task<List<ScreenshotItem>?> CheckForRemoteScreenshotsChangesAsync(string localFolder = "", CancellationToken ct = default) => Task.FromResult<List<ScreenshotItem>?>(null);

        public bool ImportCredentialsFile(string filePath) => true;
    }

    [Fact]
    public void StickyNoteService_SavesAndLoadsNotes()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"stickynote_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new StickyNoteService(tempFile);
            var initialList = service.LoadNotes();
            Assert.Empty(initialList);

            var items = new List<StickyNoteItem>
            {
                new StickyNoteItem { Title = "Catatan 1", Content = "Isi catatan pertama", Color = "Yellow" },
                new StickyNoteItem { Title = "Catatan 2", Content = "Isi catatan kedua", Color = "Green", IsPinned = true }
            };

            service.SaveNotes(items);

            var loaded = service.LoadNotes();
            Assert.Equal(2, loaded.Count);
            Assert.Equal("Catatan 1", loaded[0].Title);
            Assert.Equal("Catatan 2", loaded[1].Title);
            Assert.True(loaded[1].IsPinned);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            var deletedFile = Path.ChangeExtension(tempFile, ".deleted.json");
            if (File.Exists(deletedFile)) File.Delete(deletedFile);
        }
    }

    [Fact]
    public void StickyNoteService_TracksDeletedIds()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"stickynote_del_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new StickyNoteService(tempFile);
            service.MarkDeleted("note-id-123");
            service.MarkDeleted(new[] { "note-id-456", "note-id-789" });

            var deleted = service.GetDeletedIds();
            Assert.Contains("note-id-123", deleted);
            Assert.Contains("note-id-456", deleted);
            Assert.Contains("note-id-789", deleted);

            service.ClearDeletedIds(new[] { "note-id-123" });
            deleted = service.GetDeletedIds();
            Assert.DoesNotContain("note-id-123", deleted);
            Assert.Contains("note-id-456", deleted);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            var deletedFile = Path.ChangeExtension(tempFile, ".deleted.json");
            if (File.Exists(deletedFile)) File.Delete(deletedFile);
        }
    }

    [Fact]
    public void GoogleDriveSyncService_MergeStickyNotes_MergesCorrectlyAndRespectsTimestamps()
    {
        var baseTime = DateTime.UtcNow;

        var local = new List<StickyNoteItem>
        {
            new StickyNoteItem
            {
                Id = "1",
                Title = "Local Title 1 (Older)",
                Content = "Local Content",
                Color = "Yellow",
                UpdatedAt = baseTime.AddMinutes(-10)
            },
            new StickyNoteItem
            {
                Id = "2",
                Title = "Local Only",
                Content = "Only locally created",
                Color = "Green",
                UpdatedAt = baseTime
            }
        };

        var remote = new List<StickyNoteItem>
        {
            new StickyNoteItem
            {
                Id = "1",
                Title = "Remote Title 1 (Newer)",
                Content = "Remote Content Updated",
                Color = "Blue",
                UpdatedAt = baseTime.AddMinutes(5)
            },
            new StickyNoteItem
            {
                Id = "3",
                Title = "Remote Only",
                Content = "From another laptop",
                Color = "Pink",
                IsPinned = true,
                UpdatedAt = baseTime
            }
        };

        var merged = GoogleDriveSyncService.MergeStickyNotes(local, remote);

        Assert.Equal(3, merged.Count);

        // Pinned item (id: 3) should come first
        Assert.Equal("3", merged[0].Id);
        Assert.True(merged[0].IsPinned);

        // For item 1, remote was newer so remote title should be kept
        var item1 = merged.First(n => n.Id == "1");
        Assert.Equal("Remote Title 1 (Newer)", item1.Title);
        Assert.Equal("Blue", item1.Color);

        // Local item 2 should be preserved
        var item2 = merged.First(n => n.Id == "2");
        Assert.Equal("Local Only", item2.Title);
    }

    [Fact]
    public void GoogleDriveSyncService_MergeStickyNotes_RespectsDeletedIds()
    {
        var local = new List<StickyNoteItem>
        {
            new StickyNoteItem { Id = "1", Title = "Note 1" },
            new StickyNoteItem { Id = "2", Title = "Note 2" }
        };

        var remote = new List<StickyNoteItem>
        {
            new StickyNoteItem { Id = "1", Title = "Note 1" },
            new StickyNoteItem { Id = "2", Title = "Note 2" },
            new StickyNoteItem { Id = "3", Title = "Deleted on another device" }
        };

        var deletedIds = new[] { "3" };
        var merged = GoogleDriveSyncService.MergeStickyNotes(local, remote, deletedIds);

        Assert.Equal(2, merged.Count);
        Assert.DoesNotContain(merged, n => n.Id == "3");
    }

    [Fact]
    public void StickyNoteItemViewModel_PropertiesAndFormatting()
    {
        var model = new StickyNoteItem
        {
            Title = "Belanja Mingguan",
            Content = "1. Susu\n2. Telur\n3. Roti gandum",
            Color = "Green",
            IsPinned = true,
            UpdatedAt = DateTime.Now
        };

        var vm = new StickyNoteItemViewModel(model);

        Assert.Equal("Belanja Mingguan", vm.Title);
        Assert.Equal("Belanja Mingguan", vm.DisplayTitle);
        Assert.Equal("1. Susu", vm.Snippet);
        Assert.Equal("Hijau", vm.ColorNameIndonesia);
        Assert.True(vm.IsPinned);
        Assert.NotNull(vm.CardBackgroundBrush);
        Assert.NotNull(vm.HeaderBackgroundBrush);
        Assert.NotNull(vm.TextForegroundBrush);
        Assert.NotNull(vm.BorderBrush);

        // Change color
        vm.SetColorCommand.Execute("Pink");
        Assert.Equal("Pink", vm.Color);
        Assert.Equal("Pink", vm.ColorNameIndonesia);

        // Toggle commands
        vm.ToggleLockCommand.Execute(null);
        Assert.True(vm.IsLocked);

        vm.ToggleCollapseCommand.Execute(null);
        Assert.True(vm.IsCollapsed);
    }

    [Fact]
    public async Task MainViewModel_StickyNotes_Create_Search_Delete_And_Sync()
    {
        var tempNotesFile = Path.Combine(Path.GetTempPath(), $"mainvm_notes_{Guid.NewGuid():N}.json");
        var tempTodoFile = Path.Combine(Path.GetTempPath(), $"mainvm_todos_{Guid.NewGuid():N}.json");

        try
        {
            var settingsService = new FakeSettingsService();
            var stickyNoteService = new StickyNoteService(tempNotesFile);
            var todoService = new TodoService(tempTodoFile);
            var syncService = new FakeGoogleDriveSyncService();

            var mainVm = new MainViewModel(
                new FakeMediaSessionService(),
                new FakeLyricsService(),
                settingsService,
                todoService,
                syncService,
                stickyNoteService: stickyNoteService);

            Assert.Empty(mainVm.StickyNotes);
            Assert.Equal("0 Catatan", mainVm.StickyNotesCountText);

            // 1. Create Note
            var note1 = mainVm.CreateNewStickyNote("Yellow");
            note1.Title = "Rapat Tim";
            note1.Content = "Bahas arsitektur NOTCH";

            var note2 = mainVm.CreateNewStickyNote("Pink");
            note2.Title = "Ide Fitur";
            note2.Content = "Sticky notes sinkron drive";

            Assert.Equal(2, mainVm.StickyNotes.Count);
            Assert.Equal("2 Catatan", mainVm.StickyNotesCountText);
            Assert.True(mainVm.HasActiveStickyNotes);

            // 2. Search filtering
            mainVm.StickyNotesSearchQuery = "Rapat";
            Assert.Single(mainVm.FilteredStickyNotes);
            Assert.Equal("Rapat Tim", mainVm.FilteredStickyNotes[0].Title);

            mainVm.ClearStickyNotesSearchCommand.Execute(null);
            Assert.Equal(2, mainVm.FilteredStickyNotes.Count);

            // 3. Show all and Hide all
            mainVm.HideAllStickyNotesCommand.Execute(null);
            Assert.False(mainVm.HasActiveStickyNotes);

            mainVm.ShowAllStickyNotesCommand.Execute(null);
            Assert.True(mainVm.HasActiveStickyNotes);

            // 4. Sync with Google Drive
            await mainVm.SyncStickyNotesWithGoogleDriveAsync();
            Assert.Equal(1, syncService.SyncStickyNotesCallCount);
            // Cloud returned 1 extra note
            Assert.Equal(3, mainVm.StickyNotes.Count);
            Assert.Contains(mainVm.StickyNotes, n => n.Title == "Catatan dari Google Drive");

            // 5. Delete note
            mainVm.DeleteStickyNote(note1);
            Assert.Equal(2, mainVm.StickyNotes.Count);
            Assert.DoesNotContain(mainVm.StickyNotes, n => n.Id == note1.Id);

            var deletedIds = stickyNoteService.GetDeletedIds();
            Assert.Contains(note1.Id, deletedIds);
        }
        finally
        {
            if (File.Exists(tempNotesFile)) File.Delete(tempNotesFile);
            if (File.Exists(tempTodoFile)) File.Delete(tempTodoFile);
            var deletedNotesFile = Path.ChangeExtension(tempNotesFile, ".deleted.json");
            if (File.Exists(deletedNotesFile)) File.Delete(deletedNotesFile);
        }
    }

    [Fact]
    public void StickyNote_DefaultSize_MatchesWindowsStickyNotes()
    {
        var model = new StickyNoteItem();
        Assert.Equal(320, model.Width);
        Assert.Equal(320, model.Height);

        var vm = new StickyNoteItemViewModel(model);
        Assert.Equal(320, vm.Width);
        Assert.Equal(320, vm.Height);

        // Test clamping minimum dimensions
        vm.Width = 50;
        vm.Height = 50;
        Assert.Equal(200, vm.Width);
        Assert.Equal(160, vm.Height);

        // Test resizing to larger custom dimensions
        vm.Width = 450;
        vm.Height = 400;
        Assert.Equal(450, vm.Width);
        Assert.Equal(400, vm.Height);
    }
}
