using System.IO;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

public class TodoTests
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
        public void TriggerTrackChanged(TrackInfo? track) => TrackChanged?.Invoke(this, track);
        public void Dispose() { }
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

    [Fact]
    public void TodoService_SavesAndLoadsTodos()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new TodoService(tempFile);
            var initialList = service.LoadTodos();
            Assert.Empty(initialList);

            var items = new List<TodoItem>
            {
                new TodoItem { Title = "Beli kopi", IsCompleted = false },
                new TodoItem { Title = "Coding Notch", IsCompleted = true, CompletedAt = DateTime.Now }
            };

            service.SaveTodos(items);

            var loaded = service.LoadTodos();
            Assert.Equal(2, loaded.Count);
            Assert.Equal("Beli kopi", loaded[0].Title);
            Assert.False(loaded[0].IsCompleted);
            Assert.Equal("Coding Notch", loaded[1].Title);
            Assert.True(loaded[1].IsCompleted);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TodoItemViewModel_TogglingCompletion_UpdatesCompletedAtAndTriggersEvent()
    {
        var item = new TodoItem { Title = "Tugas Baru", IsCompleted = false };
        var vm = new TodoItemViewModel(item);
        bool changedFired = false;
        vm.OnChanged += () => changedFired = true;

        Assert.False(vm.IsCompleted);
        Assert.Null(vm.CompletedAt);

        vm.IsCompleted = true;

        Assert.True(vm.IsCompleted);
        Assert.NotNull(vm.CompletedAt);
        Assert.True(changedFired);

        var model = vm.ToModel();
        Assert.True(model.IsCompleted);
        Assert.Equal("Tugas Baru", model.Title);
    }

    [Fact]
    public void MainViewModel_AddAndManageTodos()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService);

            Assert.Equal(0, mainVm.TotalTodosCount);
            Assert.Equal(0, mainVm.PendingTodosCount);
            Assert.Equal(0, mainVm.CompletedTodosCount);
            Assert.Equal("Belum ada tugas", mainVm.TodoSummaryText);

            // Add new task
            mainVm.NewTodoTitle = "Tugas 1";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal(1, mainVm.TotalTodosCount);
            Assert.Equal(1, mainVm.PendingTodosCount);
            Assert.Equal(0, mainVm.CompletedTodosCount);
            Assert.Equal(string.Empty, mainVm.NewTodoTitle);
            Assert.Equal("0/1 selesai", mainVm.TodoSummaryText);

            // Add second task
            mainVm.NewTodoTitle = "Tugas 2";
            mainVm.AddTodoCommand.Execute(null);
            Assert.Equal(2, mainVm.TotalTodosCount);

            // Complete first task
            var firstTask = mainVm.Todos[0];
            firstTask.IsCompleted = true;

            Assert.Equal(1, mainVm.CompletedTodosCount);
            Assert.Equal(1, mainVm.PendingTodosCount);
            Assert.Equal("1/2 selesai", mainVm.TodoSummaryText);
            Assert.True(mainVm.HasCompletedTodos);

            // Test filtering
            mainVm.SetTodoFilterCommand.Execute("Active");
            Assert.Equal("Active", mainVm.TodoFilter);
            Assert.True(mainVm.IsActiveFilterSelected);

            mainVm.SetTodoFilterCommand.Execute("Completed");
            Assert.Equal("Completed", mainVm.TodoFilter);
            Assert.True(mainVm.IsCompletedFilterSelected);

            mainVm.SetTodoFilterCommand.Execute("All");
            Assert.Equal("All", mainVm.TodoFilter);
            Assert.True(mainVm.IsAllFilterSelected);

            // Clear completed
            mainVm.ClearCompletedTodosCommand.Execute(null);
            Assert.Equal(1, mainVm.TotalTodosCount);
            Assert.Equal(1, mainVm.PendingTodosCount);
            Assert.Equal(0, mainVm.CompletedTodosCount);
            Assert.Equal("Tugas 2", mainVm.Todos[0].Title);

            // Delete remaining
            mainVm.Todos[0].DeleteCommand.Execute(null);
            Assert.Equal(0, mainVm.TotalTodosCount);
            Assert.Equal("Belum ada tugas", mainVm.TodoSummaryText);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MainViewModel_TickerBehavior_RotatesAndCompletesTasks()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_ticker_test_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService);

            // Initially empty
            Assert.Equal("Belum ada tugas", mainVm.CurrentTickerText);
            Assert.False(mainVm.HasPendingTodos);

            // Add 3 tasks
            mainVm.NewTodoTitle = "Beli Susu";
            mainVm.AddTodoCommand.Execute(null);
            mainVm.NewTodoTitle = "Meeting Project";
            mainVm.AddTodoCommand.Execute(null);
            mainVm.NewTodoTitle = "Olahraga";
            mainVm.AddTodoCommand.Execute(null);

            Assert.True(mainVm.HasPendingTodos);
            Assert.Contains("Beli Susu", mainVm.CurrentTickerText);
            Assert.Contains("Meeting Project", mainVm.CurrentTickerText);
            Assert.Contains("Olahraga", mainVm.CurrentTickerText);
            Assert.Equal("3", mainVm.CurrentTickerBadge);

            // Complete current item
            mainVm.CompleteCurrentTickerCommand.Execute(null);
            Assert.Equal(2, mainVm.PendingTodosCount);
            Assert.Equal(1, mainVm.CompletedTodosCount);
            Assert.DoesNotContain("Beli Susu", mainVm.CurrentTickerText);
            Assert.Contains("Meeting Project", mainVm.CurrentTickerText);

            // Complete remaining tasks
            mainVm.CompleteCurrentTickerCommand.Execute(null);
            mainVm.CompleteCurrentTickerCommand.Execute(null);

            Assert.Equal(0, mainVm.PendingTodosCount);
            Assert.False(mainVm.HasPendingTodos);
            Assert.Equal("Semua tugas selesai 🎉", mainVm.CurrentTickerText);
            Assert.Equal("✓", mainVm.CurrentTickerBadge);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void GoogleDriveSyncService_MergeTodos_MergesCorrectly()
    {
        var local = new List<TodoItem>
        {
            new TodoItem { Id = "1", Title = "Task 1", IsCompleted = true },
            new TodoItem { Id = "2", Title = "Task 2 Local", IsCompleted = false }
        };

        var remote = new List<TodoItem>
        {
            new TodoItem { Id = "1", Title = "Task 1", IsCompleted = false },
            new TodoItem { Id = "3", Title = "Task 3 Remote", IsCompleted = false }
        };

        var merged = GoogleDriveSyncService.MergeTodos(local, remote);

        Assert.Equal(3, merged.Count);
        // Task 1 should be completed because local has it completed
        var item1 = merged.First(m => m.Id == "1");
        Assert.True(item1.IsCompleted);

        // Task 2 from local and Task 3 from remote should both exist
        Assert.Contains(merged, m => m.Id == "2" && m.Title == "Task 2 Local");
        Assert.Contains(merged, m => m.Id == "3" && m.Title == "Task 3 Remote");
    }

    [Fact]
    public void GoogleDriveSyncService_MergeTodos_RespectsDeletedIds_DoesNotRestoreDeletedItem()
    {
        var local = new List<TodoItem>
        {
            new TodoItem { Id = "2", Title = "Task 2", IsCompleted = false }
        };

        var remote = new List<TodoItem>
        {
            new TodoItem { Id = "1", Title = "Task 1 (Deleted)", IsCompleted = false },
            new TodoItem { Id = "2", Title = "Task 2", IsCompleted = false },
            new TodoItem { Id = "3", Title = "Task 3 (New from remote)", IsCompleted = false }
        };

        var deletedIds = new[] { "1" };

        var merged = GoogleDriveSyncService.MergeTodos(local, remote, deletedIds);

        Assert.Equal(2, merged.Count);
        Assert.DoesNotContain(merged, m => m.Id == "1");
        Assert.Contains(merged, m => m.Id == "2");
        Assert.Contains(merged, m => m.Id == "3");
    }

    [Fact]
    public void TodoService_DeletedIds_PersistsAndClears()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_test_{Guid.NewGuid():N}.json");
        var tempDeleted = Path.ChangeExtension(tempFile, ".deleted.json");
        try
        {
            var service = new TodoService(tempFile);
            Assert.Empty(service.GetDeletedIds());

            service.MarkDeleted("item-1");
            service.MarkDeleted(new[] { "item-2", "item-3" });

            var deleted = service.GetDeletedIds();
            Assert.Equal(3, deleted.Count);
            Assert.Contains("item-1", deleted);
            Assert.Contains("item-2", deleted);
            Assert.Contains("item-3", deleted);

            var service2 = new TodoService(tempFile);
            var reloaded = service2.GetDeletedIds();
            Assert.Equal(3, reloaded.Count);

            service2.ClearDeletedIds(new[] { "item-2" });
            Assert.Equal(2, service2.GetDeletedIds().Count);
            Assert.DoesNotContain("item-2", service2.GetDeletedIds());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempDeleted)) File.Delete(tempDeleted);
        }
    }

    [Fact]
    public void GoogleDriveSyncService_ImportCredentials_ParsesValidJson()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"credentials_test_{Guid.NewGuid():N}.json");
        try
        {
            var fakeJson = """
            {
              "installed": {
                "client_id": "test-client-id-123.apps.googleusercontent.com",
                "client_secret": "test-secret-456"
              }
            }
            """;
            File.WriteAllText(tempFile, fakeJson);

            var settingsService = new FakeSettingsService();
            var syncService = new GoogleDriveSyncService(settingsService);

            var success = syncService.ImportCredentialsFile(tempFile);
            Assert.True(success);
            Assert.Equal("test-client-id-123.apps.googleusercontent.com", settingsService.CurrentSettings.GoogleDriveClientId);
            Assert.Equal("test-secret-456", settingsService.CurrentSettings.GoogleDriveClientSecret);
            Assert.True(syncService.IsConfigured);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void GoogleDriveSyncService_EnvironmentVariables_ResolveCorrectly()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.GoogleDriveClientId = string.Empty;
        settingsService.CurrentSettings.GoogleDriveClientSecret = string.Empty;

        var prevId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID");
        var prevSecret = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET");

        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID", "env-client-id-999");
            Environment.SetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET", "env-secret-888");

            var syncService = new GoogleDriveSyncService(settingsService);

            Assert.Equal("env-client-id-999", syncService.EffectiveClientId);
            Assert.Equal("env-secret-888", syncService.EffectiveClientSecret);
            Assert.True(syncService.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID", prevId);
            Environment.SetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET", prevSecret);
        }
    }

    private class FakeGoogleDriveSyncService : IGoogleDriveSyncService
    {
        public bool IsConfigured { get; set; } = true;
        public bool IsConnected { get; set; } = true;
        public bool IsSyncing { get; set; } = false;
        public string UserEmail { get; set; } = "user@gmail.com";
        public DateTime? LastSyncTime { get; set; } = DateTime.Now;
        public string SyncStatus { get; set; } = "Tersambung";
        public int UploadCallCount { get; set; }

        public event Action? StatusChanged;
        public event Action<List<TodoItem>>? TodosSynced;
        public void TriggerStatusChanged() => StatusChanged?.Invoke();
        public void TriggerTodosSynced(List<TodoItem> items) => TodosSynced?.Invoke(items);

        public Task<bool> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default)
        {
            UploadCallCount++;
            return Task.FromResult(true);
        }
        public Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(new List<TodoItem>());
        public Task<List<TodoItem>?> CheckForRemoteChangesAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(null);
        public Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, IEnumerable<string>? deletedIds = null, CancellationToken ct = default)
        {
            var list = localTodos.ToList();
            list.Add(new TodoItem { Title = "Cloud Synced Task", IsCompleted = false });
            return Task.FromResult<List<TodoItem>?>(list);
        }
        public bool ImportCredentialsFile(string filePath) => true;
    }

    [Fact]
    public async Task MainViewModel_GoogleDriveSync_SyncsTasks()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_gdrive_vm_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();
            var syncService = new FakeGoogleDriveSyncService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService, syncService);

            Assert.True(mainVm.IsGoogleDriveConnected);

            // Add local task
            mainVm.NewTodoTitle = "Local Task";
            mainVm.AddTodoCommand.Execute(null);

            // Sync with Google Drive
            await mainVm.SyncWithGoogleDriveAsync();

            // Synced task should be in list
            Assert.Equal(2, mainVm.TotalTodosCount);
            Assert.Contains(mainVm.Todos, t => t.Title == "Cloud Synced Task");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task MainViewModel_GoogleDrive_DetectsRemoteChangesAutomatically()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_gdrive_poll_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();
            var syncService = new FakeGoogleDriveSyncService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService, syncService);

            // Add local task
            mainVm.NewTodoTitle = "Local Task 1";
            mainVm.AddTodoCommand.Execute(null);
            Assert.Single(mainVm.Todos);

            // Simulate remote machine adding a task and pushing to Google Drive
            var remoteList = new List<TodoItem>
            {
                new TodoItem { Id = mainVm.Todos[0].Id, Title = "Local Task 1", IsCompleted = false },
                new TodoItem { Id = Guid.NewGuid().ToString(), Title = "Task Created on Machine B", IsCompleted = false }
            };

            // Trigger sync event (as CheckForRemoteChangesAsync does when remote change is detected)
            syncService.TriggerTodosSynced(remoteList);

            // Machine A should now automatically have 2 tasks without clicking sync!
            Assert.Equal(2, mainVm.TotalTodosCount);
            Assert.Contains(mainVm.Todos, t => t.Title == "Task Created on Machine B");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("MARIBON: twibbon hari santri", "MARIBON", "twibbon hari santri")]
    [InlineData("KULIAH: tugas kalkulus", "KULIAH", "tugas kalkulus")]
    [InlineData("WORK: project: subtask", "WORK", "project: subtask")]
    [InlineData("Beli kopi", "", "Beli kopi")]
    [InlineData("   SPACES:   task with spaces   ", "SPACES", "task with spaces")]
    public void TodoItem_ParseCategory_ExtractsCategoryAndTask(string raw, string expectedCategory, string expectedTask)
    {
        var (category, taskText) = TodoItem.ParseCategory(raw);
        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedTask, taskText);
    }

    [Fact]
    public void TodoItemViewModel_CategoryProperties_StayInSync()
    {
        var item = new TodoItem { Title = "MARIBON: twibbon hari santri" };
        var vm = new TodoItemViewModel(item);

        Assert.True(vm.HasCategory);
        Assert.Equal("MARIBON", vm.Category);
        Assert.Equal("twibbon hari santri", vm.TaskText);
        Assert.Equal("MARIBON: twibbon hari santri", vm.Title);

        // Update task text
        vm.TaskText = "twibbon hari guru";
        Assert.Equal("MARIBON: twibbon hari guru", vm.Title);

        // Update category
        vm.Category = "DESAIN";
        Assert.Equal("DESAIN: twibbon hari guru", vm.Title);

        // Update title directly
        vm.Title = "KULIAH: Makalah Sejarah";
        Assert.Equal("KULIAH", vm.Category);
        Assert.Equal("Makalah Sejarah", vm.TaskText);

        // Update title without category
        vm.Title = "Beli susu";
        Assert.False(vm.HasCategory);
        Assert.Equal(string.Empty, vm.Category);
        Assert.Equal("Beli susu", vm.TaskText);
    }

    [Fact]
    public void MainViewModel_CategoryFiltering_FiltersItemsAndUpdatesTicker()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_cat_test_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService);

            // Add tagged and untagged items
            mainVm.NewTodoTitle = "MARIBON: twibbon hari santri";
            mainVm.AddTodoCommand.Execute(null);

            mainVm.NewTodoTitle = "MARIBON: poster dakwah";
            mainVm.AddTodoCommand.Execute(null);

            mainVm.NewTodoTitle = "KULIAH: tugas kalkulus";
            mainVm.AddTodoCommand.Execute(null);

            mainVm.NewTodoTitle = "Beli kopi";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal(4, mainVm.TotalTodosCount);
            Assert.True(mainVm.HasCategories);
            Assert.Contains("MARIBON", mainVm.AvailableCategories);
            Assert.Contains("KULIAH", mainVm.AvailableCategories);

            // Filter by MARIBON
            mainVm.SetCategoryFilterCommand.Execute("MARIBON");
            Assert.Equal("MARIBON", mainVm.SelectedCategoryFilter);

            var filtered = mainVm.FilteredTodos.Cast<TodoItemViewModel>().ToList();
            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, item => Assert.Equal("MARIBON", item.Category));

            // Filter by All
            mainVm.SetCategoryFilterCommand.Execute("All");
            var allFiltered = mainVm.FilteredTodos.Cast<TodoItemViewModel>().ToList();
            Assert.Equal(4, allFiltered.Count);

            // Verify Ticker displays Category and TaskText in continuous stream
            mainVm.UpdateTicker();
            Assert.NotNull(mainVm.CurrentTickerItem);
            Assert.Contains("[MARIBON] twibbon hari santri", mainVm.CurrentTickerText);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MainViewModel_NewTodoCategory_CombinesCategoryWithTitle()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_cat_select_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mediaService = new FakeMediaSessionService();
            var lyricsService = new FakeLyricsService();
            var settingsService = new FakeSettingsService();

            var mainVm = new MainViewModel(mediaService, lyricsService, settingsService, todoService);

            // User selects category from dropdown/selector
            mainVm.NewTodoCategory = "MARIBON";
            mainVm.NewTodoTitle = "twibbon hari santri";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal(1, mainVm.TotalTodosCount);
            var item = mainVm.Todos[0];
            Assert.Equal("MARIBON", item.Category);
            Assert.Equal("twibbon hari santri", item.TaskText);
            Assert.Equal("MARIBON: twibbon hari santri", item.Title);

            // Check ExistingCategories contains MARIBON
            Assert.Contains("MARIBON", mainVm.ExistingCategories);

            // Add second item with different category selected
            mainVm.NewTodoCategory = "KULIAH";
            mainVm.NewTodoTitle = "tugas aljabar";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal(2, mainVm.TotalTodosCount);
            var item2 = mainVm.Todos[1];
            Assert.Equal("KULIAH", item2.Category);
            Assert.Equal("tugas aljabar", item2.TaskText);

            // Add third item with Tanpa Kategori
            mainVm.NewTodoCategory = "(Tanpa Kategori)";
            mainVm.NewTodoTitle = "beli sabun";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal(3, mainVm.TotalTodosCount);
            var item3 = mainVm.Todos[2];
            Assert.False(item3.HasCategory);
            Assert.Equal("beli sabun", item3.TaskText);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void SettingsViewModel_ComponentDisplayMode_And_Detach_TogglesCorrectly()
    {
        var settingsService = new FakeSettingsService();
        var vm = new SettingsViewModel(settingsService, _ => { });

        Assert.Equal("Both", vm.ComponentDisplayMode);
        Assert.True(vm.IsComponentBoth);
        Assert.False(vm.IsComponentMusicOnly);
        Assert.False(vm.IsComponentTodoOnly);
        Assert.False(vm.IsTodoDetached);

        // Switch to MusicOnly
        vm.IsComponentMusicOnly = true;
        Assert.Equal("MusicOnly", vm.ComponentDisplayMode);
        Assert.False(vm.IsComponentBoth);
        Assert.True(vm.IsComponentMusicOnly);
        Assert.False(vm.IsComponentTodoOnly);

        // Switch to TodoOnly
        vm.IsComponentTodoOnly = true;
        Assert.Equal("TodoOnly", vm.ComponentDisplayMode);
        Assert.False(vm.IsComponentBoth);
        Assert.False(vm.IsComponentMusicOnly);
        Assert.True(vm.IsComponentTodoOnly);

        // Switch to Both and Detach
        vm.IsComponentBoth = true;
        vm.IsTodoDetached = true;
        Assert.True(vm.IsTodoDetached);
        Assert.True(settingsService.CurrentSettings.IsTodoDetached);
    }

    [Fact]
    public void MainViewModel_ComponentDisplayMode_UpdatesLayoutState()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.EnablePrayerTimes = false;
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService);

        // Default: Both & Attached
        Assert.True(mainVm.ShowMusicSection);
        Assert.True(mainVm.ShowTodoSection);
        Assert.True(mainVm.ShowTodoTicker);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.True(mainVm.ShowSplitter);
        Assert.Equal(845, mainVm.ExpandedPanelWidth);

        // Detach Todo
        mainVm.DetachTodoCommand.Execute(null);
        Assert.True(mainVm.IsTodoDetached);
        Assert.False(mainVm.ShowTodoTicker);
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowSplitter);
        Assert.Equal(410, mainVm.ExpandedPanelWidth);

        // Re-attach Todo
        mainVm.AttachTodoCommand.Execute(null);
        Assert.False(mainVm.IsTodoDetached);
        Assert.True(mainVm.ShowTodoTicker);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.True(mainVm.ShowSplitter);
        Assert.Equal(845, mainVm.ExpandedPanelWidth);

        // Music Only
        mainVm.ComponentDisplayMode = "MusicOnly";
        Assert.True(mainVm.ShowMusicSection);
        Assert.False(mainVm.ShowTodoSection);
        Assert.False(mainVm.ShowTodoTicker);
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.Equal(410, mainVm.ExpandedPanelWidth);

        // Todo Only
        mainVm.ComponentDisplayMode = "TodoOnly";
        Assert.False(mainVm.ShowMusicSection);
        Assert.True(mainVm.ShowTodoSection);
        Assert.True(mainVm.ShowTodoTicker);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowSplitter);
        Assert.Equal(410, mainVm.ExpandedPanelWidth);
    }

    [Fact]
    public void MainViewModel_ShowMusicMinimizeButton_UpdatesCorrectlyWhenSeparated()
    {
        var settingsService = new FakeSettingsService();
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService);

        // Initially both combined: Music does not need separate minimize button (To-Do section at right has it)
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowMusicMinimizeButton);

        // When To-Do is detached / separated: Music component MUST have minimize button
        mainVm.DetachTodoCommand.Execute(null);
        Assert.True(mainVm.IsTodoDetached);
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.True(mainVm.ShowMusicMinimizeButton);

        // When To-Do is re-attached: Music minimize button returns to combined mode
        mainVm.AttachTodoCommand.Execute(null);
        Assert.False(mainVm.IsTodoDetached);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowMusicMinimizeButton);

        // When ComponentDisplayMode is MusicOnly: Music component has minimize button
        mainVm.ComponentDisplayMode = "MusicOnly";
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.True(mainVm.ShowMusicMinimizeButton);
    }

    [Fact]
    public void MainViewModel_TodoWindowCompactCommands_ToggleCorrectly()
    {
        var settingsService = new FakeSettingsService();
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService);

        // Default: expanded
        Assert.False(mainVm.IsTodoWindowCompact);
        Assert.True(mainVm.IsTodoWindowExpanded);

        // Minimize command -> compact
        mainVm.MinimizeTodoWindowCommand.Execute(null);
        Assert.True(mainVm.IsTodoWindowCompact);
        Assert.False(mainVm.IsTodoWindowExpanded);
        Assert.True(settingsService.CurrentSettings.IsTodoWindowCompact);

        // Expand command -> expanded
        mainVm.ExpandTodoWindowCommand.Execute(null);
        Assert.False(mainVm.IsTodoWindowCompact);
        Assert.True(mainVm.IsTodoWindowExpanded);
        Assert.False(settingsService.CurrentSettings.IsTodoWindowCompact);
    }

    [Fact]
    public void TodoWindow_InitializesWithoutErrors()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new FakeSettingsService();
                var mainVm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settings);
                var window = new Views.TodoWindow(mainVm, settings);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(5000);

        Assert.True(finished);
        Assert.Null(caught);
    }

    [Fact]
    public async Task MainViewModel_AutoSync_TriggersUploadOnTodoChanges()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.GoogleDriveAutoSync = true;
        var syncService = new FakeGoogleDriveSyncService { IsConnected = true };

        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_autosync_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var mainVm = new MainViewModel(
                new FakeMediaSessionService(),
                new FakeLyricsService(),
                settingsService,
                todoService: todoService,
                googleDriveSyncService: syncService);

            Assert.Equal(0, syncService.UploadCallCount);

            // Add a todo
            mainVm.NewTodoTitle = "Tugas Autosync";
            mainVm.AddTodoCommand.Execute(null);

            // Wait for debounce timer (700ms)
            await Task.Delay(1100);
            Assert.True(syncService.UploadCallCount >= 1, "Upload should be called after adding todo");

            int previousCount = syncService.UploadCallCount;

            // Toggle completion
            mainVm.Todos[0].IsCompleted = true;
            await Task.Delay(1100);
            Assert.True(syncService.UploadCallCount > previousCount, "Upload should be called after toggling completion");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MainViewModel_MoveTodo_ReordersAndPersists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_reorder_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var settingsService = new FakeSettingsService();
            var mainVm = new MainViewModel(
                new FakeMediaSessionService(),
                new FakeLyricsService(),
                settingsService,
                todoService: todoService);

            mainVm.NewTodoTitle = "Task A";
            mainVm.AddTodoCommand.Execute(null);
            mainVm.NewTodoTitle = "Task B";
            mainVm.AddTodoCommand.Execute(null);
            mainVm.NewTodoTitle = "Task C";
            mainVm.AddTodoCommand.Execute(null);

            Assert.Equal("Task A", mainVm.Todos[0].Title);
            Assert.Equal("Task B", mainVm.Todos[1].Title);
            Assert.Equal("Task C", mainVm.Todos[2].Title);

            // Move Task C to index 0 (drag naik ke paling atas)
            var itemC = mainVm.Todos[2];
            var itemA = mainVm.Todos[0];
            mainVm.MoveTodo(itemC, itemA);

            Assert.Equal("Task C", mainVm.Todos[0].Title);
            Assert.Equal("Task A", mainVm.Todos[1].Title);
            Assert.Equal("Task B", mainVm.Todos[2].Title);

            // Reload from service to verify persistence
            var reloaded = todoService.LoadTodos().ToList();
            Assert.Equal("Task C", reloaded[0].Title);
            Assert.Equal("Task A", reloaded[1].Title);
            Assert.Equal("Task B", reloaded[2].Title);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void MarqueeTextBlock_InitializesInSTA()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                var marquee = new Helpers.MarqueeTextBlock
                {
                    Text = "Tugas Marquee Kiri ke Kanan",
                    FontSize = 12,
                    Speed = 40
                };
                marquee.Measure(new System.Windows.Size(200, 30));
                marquee.Arrange(new System.Windows.Rect(0, 0, 200, 30));
                marquee.RestartAnimation(initialStartAtZero: true);
                marquee.Pause();
                marquee.Resume();
                marquee.StopAnimation();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(5000);

        Assert.True(finished);
        Assert.Null(caught);
    }

    [Fact]
    public void MainViewModel_HasTrack_ReflectsTrackAvailability()
    {
        var mediaService = new FakeMediaSessionService();
        var lyricsService = new FakeLyricsService();
        var settingsService = new FakeSettingsService();
        var todoService = new TodoService(settingsService);

        var vm = new MainViewModel(mediaService, lyricsService, settingsService, todoService);

        // Initially no track
        Assert.False(vm.HasTrack);
        Assert.Equal(string.Empty, vm.ActiveLyric);

        // Track with title
        mediaService.TriggerTrackChanged(new TrackInfo
        {
            Title = "Song Title",
            Artist = "Artist"
        });

        Assert.True(vm.HasTrack);

        // Track null
        mediaService.TriggerTrackChanged(null);
        Assert.False(vm.HasTrack);
        Assert.Equal(string.Empty, vm.ActiveLyric);
    }

    [Fact]
    public async Task MainViewModel_GoogleDriveStatus_ReturnsCorrectLabelsAndTooltips()
    {
        var mediaService = new FakeMediaSessionService();
        var lyricsService = new FakeLyricsService();
        var settingsService = new FakeSettingsService();
        var todoService = new TodoService(settingsService);
        var syncService = new FakeGoogleDriveSyncService { IsConnected = false, UserEmail = string.Empty };

        var vm = new MainViewModel(mediaService, lyricsService, settingsService, todoService, syncService);

        Assert.False(vm.IsGoogleDriveConnected);
        Assert.Equal("Drive: Belum Konek", vm.GoogleDriveStatusButtonText);
        Assert.Contains("belum terhubung", vm.GoogleDriveSyncTooltip);

        // Trigger click when disconnected -> authenticates
        await vm.HandleGoogleDriveClickAsync();
        // FakeGoogleDriveSyncService AuthenticateAsync returns true
        syncService.IsConnected = true;
        syncService.UserEmail = "test@example.com";
        syncService.TriggerStatusChanged();

        Assert.True(vm.IsGoogleDriveConnected);
        Assert.Equal("Drive", vm.GoogleDriveStatusButtonText);
        Assert.Contains("test@example.com", vm.GoogleDriveSyncTooltip);
    }

    [Fact]
    public void TodoItemViewModel_Edit_StartSaveCancelWorksCorrectly()
    {
        var item = new TodoItem { Title = "Kategori: Tugas Awal" };
        var vm = new TodoItemViewModel(item);

        Assert.False(vm.IsEditing);
        Assert.Equal("Kategori", vm.Category);
        Assert.Equal("Tugas Awal", vm.TaskText);

        // Start Edit
        vm.StartEdit();
        Assert.True(vm.IsEditing);
        Assert.Equal("Kategori: Tugas Awal", vm.EditText);

        // Cancel Edit
        vm.EditText = "Tugas Berubah";
        vm.CancelEdit();
        Assert.False(vm.IsEditing);
        Assert.Equal("Kategori: Tugas Awal", vm.Title);

        // Save Edit
        vm.StartEdit();
        vm.EditText = "Projek: Tugas Direvisi";
        bool changed = false;
        vm.OnChanged += () => changed = true;
        vm.SaveEdit();

        Assert.False(vm.IsEditing);
        Assert.True(changed);
        Assert.Equal("Projek: Tugas Direvisi", vm.Title);
        Assert.Equal("Projek", vm.Category);
        Assert.Equal("Tugas Direvisi", vm.TaskText);
    }

    [Fact]
    public void MainViewModel_AutoSync_TriggeredOnAddEditAndDelete()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var settingsService = new FakeSettingsService();
            settingsService.CurrentSettings.GoogleDriveAutoSync = true;
            var syncService = new FakeGoogleDriveSyncService { IsConnected = true };

            var tempFile = Path.Combine(Path.GetTempPath(), $"dranotch_autosync_crud_{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(tempFile, "[]");
                var todoService = new TodoService(tempFile);
                var vm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settingsService,
                    todoService: todoService,
                    googleDriveSyncService: syncService);

                int initialCalls = syncService.UploadCallCount;

                // 1. ADD
                vm.NewTodoTitle = "Tugas Baru";
                vm.AddTodoCommand.Execute(null);

                Thread.Sleep(850);
                Assert.True(syncService.UploadCallCount > initialCalls, "Upload should be called on Add");
                int afterAdd = syncService.UploadCallCount;

                // 2. EDIT / UPDATE
                var addedItem = vm.Todos.First();
                addedItem.StartEdit();
                addedItem.EditText = "Tugas Diedit";
                addedItem.SaveEdit();

                Thread.Sleep(850);
                Assert.True(syncService.UploadCallCount > afterAdd, "Upload should be called on Edit");
                int afterEdit = syncService.UploadCallCount;

                // 3. DELETE
                var deletedId = addedItem.Id;
                addedItem.DeleteCommand.Execute(null);

                Thread.Sleep(850);
                Assert.True(syncService.UploadCallCount > afterEdit, "Upload should be called on Delete");
                Assert.Contains(deletedId, todoService.GetDeletedIds());
            }
            catch (Exception ex)
            {
                caught = ex;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(10000);
        Assert.True(finished);
        Assert.Null(caught);
    }

    [Fact]
    public void MainViewModel_Hover_DoesNotImmediatelyExpand()
    {
        var settingsService = new FakeSettingsService();
        settingsService.CurrentSettings.ExpandOnHover = true;

        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settingsService,
            new TodoService(settingsService));

        vm.CurrentMode = PanelMode.Compact;

        bool expandedImmediately = false;
        vm.RequestExpand += () => expandedImmediately = true;

        vm.OnMouseEnter();

        // Should NOT expand immediately upon cursor entering
        Assert.False(expandedImmediately, "Hover should not expand the panel immediately upon mouse enter");

        // Leaving mouse should cancel the expansion timer
        vm.OnMouseLeave();
        Assert.False(expandedImmediately);
    }

    [Fact]
    public void TodoItem_DueDate_SerializationAndDeserialization()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"todo_due_{Guid.NewGuid()}.json");
        try
        {
            var service = new TodoService(tempFile);
            var expectedDue = new DateTime(2026, 10, 26, 17, 30, 0);
            var items = new List<TodoItem>
            {
                new TodoItem
                {
                    Title = "Kerja: Kirim laporan keuangan",
                    DueDate = expectedDue,
                    IsDueNotified = false
                }
            };

            service.SaveTodos(items);
            var loaded = service.LoadTodos();

            Assert.Single(loaded);
            Assert.Equal("Kerja: Kirim laporan keuangan", loaded[0].Title);
            Assert.True(loaded[0].DueDate.HasValue);
            Assert.Equal((DateTime?)expectedDue, loaded[0].DueDate);
            Assert.False(loaded[0].IsDueNotified);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TodoItemViewModel_DueDateProperties_FormatCorrectly()
    {
        var dueToday = DateTime.Today.AddHours(18).AddMinutes(15);
        var item = new TodoItem
        {
            Title = "Belajar: Baca bab 3",
            DueDate = dueToday
        };

        var vm = new TodoItemViewModel(item);
        Assert.True(vm.HasDueDate);
        Assert.Equal("Hari ini 18:15", vm.DueDateText);
        Assert.False(vm.IsOverdue);

        // Test overdue
        var duePast = DateTime.Now.AddHours(-2);
        vm.DueDate = duePast;
        Assert.True(vm.IsOverdue);
        Assert.Contains("#3A141A", vm.DueBadgeBackground);

        // Once completed, it is no longer marked overdue
        vm.IsCompleted = true;
        Assert.False(vm.IsOverdue);
    }

    [Fact]
    public void MainViewModel_AddTodo_WithDueDate_AndCheckDeadline()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"todo_vm_{Guid.NewGuid()}.json");
            try
            {
                var settingsService = new FakeSettingsService();
                var todoService = new TodoService(tempFile);
                var vm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settingsService,
                    todoService);

                var dueTime = DateTime.Now.AddMinutes(-1); // already reached deadline
                vm.NewTodoTitle = "Kirim dokumen proyek";
                vm.NewTodoDueDate = dueTime;

                Assert.True(vm.HasNewTodoDueDate);
                vm.AddTodoCommand.Execute(null);

                Assert.Single(vm.Todos);
                Assert.Equal("Kirim dokumen proyek", vm.Todos[0].Title);
                Assert.Equal(dueTime, vm.Todos[0].DueDate);
                Assert.False(vm.HasNewTodoDueDate); // reset after add

                string? trayNotifTitle = null;
                string? trayNotifMsg = null;
                vm.RequestShowTrayNotification += (t, m) =>
                {
                    trayNotifTitle = t;
                    trayNotifMsg = m;
                };

                // Trigger deadline check
                vm.CheckTodoDeadlines();

                Assert.True(vm.Todos[0].IsDueNotified);
                Assert.True(vm.HasActiveTodoNotification);
                Assert.NotNull(vm.ActiveTodoNotification);
                Assert.Equal("⏰ Batas Waktu To-Do!", vm.ActiveTodoNotification.Title);
                Assert.Contains("Kirim dokumen proyek", vm.ActiveTodoNotification.Message);
                Assert.NotNull(trayNotifTitle);

                // Complete through notification action button
                vm.CompleteTodoNotificationCommand.Execute(null);
                Assert.True(vm.Todos[0].IsCompleted);
                Assert.False(vm.HasActiveTodoNotification);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(10000);
        Assert.True(finished);
        Assert.Null(caught);
    }

    [Fact]
    public void TodoItemViewModel_InlineEdit_CanModifyAndCancelDueDate()
    {
        var originalDue = new DateTime(2026, 10, 26, 17, 0, 0);
        var item = new TodoItem
        {
            Title = "Kerja: Desain poster",
            DueDate = originalDue
        };

        var vm = new TodoItemViewModel(item);
        Assert.Equal(originalDue, vm.DueDate);

        // Start edit
        vm.StartEdit();
        Assert.True(vm.IsEditing);
        Assert.Equal(originalDue, vm.EditDueDate);
        Assert.True(vm.HasEditDueDate);

        // Change preset during edit
        vm.SetEditDueDatePreset("tomorrow_09");
        var tomorrow9 = new DateTime(DateTime.Today.AddDays(1).Year, DateTime.Today.AddDays(1).Month, DateTime.Today.AddDays(1).Day, 9, 0, 0);
        Assert.Equal(tomorrow9, vm.EditDueDate);
        // Original DueDate should still be untouched before save
        Assert.Equal(originalDue, vm.DueDate);

        // Cancel edit -> reverts EditDueDate
        vm.CancelEdit();
        Assert.False(vm.IsEditing);
        Assert.Equal(originalDue, vm.DueDate);

        // Start edit again and save
        vm.StartEdit();
        vm.EditText = "Kerja: Desain poster banner";
        vm.SetEditDueDatePreset("today_21");
        var today21 = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 21, 0, 0);
        vm.SaveEdit();

        Assert.False(vm.IsEditing);
        Assert.Equal("Kerja: Desain poster banner", vm.Title);
        Assert.Equal(today21, vm.DueDate);

        // Clear due date in edit mode
        vm.StartEdit();
        vm.ClearEditDueDateCommand.Execute(null);
        Assert.False(vm.HasEditDueDate);
        Assert.Null(vm.EditDueDate);
        vm.SaveEdit();

        Assert.False(vm.HasDueDate);
        Assert.Null(vm.DueDate);
    }

    [Fact]
    public void TodoItemViewModel_InlineEdit_WindowIsolationAndCustomDeadline()
    {
        var item = new TodoItem
        {
            Title = "Kerja: Desain banner",
            DueDate = null
        };
        var vm = new TodoItemViewModel(item);
        vm.StartEdit();

        Assert.False(vm.IsSettingEditDueDateInDetached);
        Assert.False(vm.IsSettingEditDueDateInMain);
        Assert.False(vm.IsSettingEditDueDate);

        // Open in detached window only
        vm.ToggleEditDueDateDetachedCommand.Execute(null);
        Assert.True(vm.IsSettingEditDueDateInDetached);
        Assert.False(vm.IsSettingEditDueDateInMain);
        Assert.True(vm.IsSettingEditDueDate);

        // Switch to main window -> detached closes, main opens
        vm.ToggleEditDueDateMainCommand.Execute(null);
        Assert.False(vm.IsSettingEditDueDateInDetached);
        Assert.True(vm.IsSettingEditDueDateInMain);
        Assert.True(vm.IsSettingEditDueDate);

        // Test custom deadline setting
        var targetDay = new DateTime(2026, 11, 15);
        vm.EditCustomDeadlineDate = targetDay;
        vm.EditCustomDeadlineHour = 14;
        vm.EditCustomDeadlineMinute = 30;

        vm.ApplyEditCustomDeadlineCommand.Execute(null);

        Assert.False(vm.IsSettingEditDueDateInDetached);
        Assert.False(vm.IsSettingEditDueDateInMain);
        Assert.False(vm.IsSettingEditDueDate);
        Assert.NotNull(vm.EditDueDate);
        Assert.Equal(new DateTime(2026, 11, 15, 14, 30, 0), vm.EditDueDate.Value);

        // Save edit
        vm.SaveEdit();
        Assert.Equal(new DateTime(2026, 11, 15, 14, 30, 0), vm.DueDate);
    }

    [Fact]
    public void TodoItemViewModel_CategoryGroup_ReturnsCategoryOrTanpaKategori()
    {
        var item1 = new TodoItem { Title = "Tugas biasa" };
        var vm1 = new TodoItemViewModel(item1);
        Assert.Equal("Tanpa Kategori", vm1.CategoryGroup);

        var item2 = new TodoItem { Title = "Kerja: Desain UI" };
        var vm2 = new TodoItemViewModel(item2);
        Assert.Equal("Kerja", vm2.CategoryGroup);

        bool categoryGroupNotified = false;
        vm1.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TodoItemViewModel.CategoryGroup))
                categoryGroupNotified = true;
        };

        vm1.Category = "Pribadi";
        Assert.True(categoryGroupNotified);
        Assert.Equal("Pribadi", vm1.CategoryGroup);
    }

    [Fact]
    public void TodoItemViewModel_RemainingTimeText_CalculatesCorrectly()
    {
        var now = DateTime.Now;
        var item = new TodoItem
        {
            Title = "Testing Deadline",
            DueDate = now.AddDays(2).AddHours(3).AddMinutes(1)
        };
        var vm = new TodoItemViewModel(item);

        // Upcoming > 1 day
        Assert.StartsWith("Sisa 2 hr", vm.RemainingTimeText);
        Assert.Contains(vm.DueDateText, vm.DueDateBadgeText);
        Assert.Contains("Sisa 2 hr", vm.DueDateBadgeText);

        // Upcoming < 1 day
        vm.DueDate = now.AddHours(3).AddMinutes(15);
        Assert.StartsWith("Sisa 3 jam", vm.RemainingTimeText);

        // Upcoming < 1 hour
        vm.DueDate = now.AddMinutes(45);
        Assert.StartsWith("Sisa 4", vm.RemainingTimeText);

        // Overdue
        vm.DueDate = now.AddHours(-2).AddMinutes(-5);
        Assert.StartsWith("Terlewat 2 jam", vm.RemainingTimeText);
        Assert.Contains("Terlewat 2 jam", vm.DueDateBadgeText);

        // Completed
        vm.IsCompleted = true;
        Assert.Equal("Selesai", vm.RemainingTimeText);
        Assert.Contains("(Selesai)", vm.DueDateBadgeText);

        // Null deadline
        vm.DueDate = null;
        Assert.Equal(string.Empty, vm.RemainingTimeText);
        Assert.Equal(string.Empty, vm.DueDateBadgeText);
    }

    [Fact]
    public void MainViewModel_MoveTodo_CrossCategory_InheritsTargetCategory()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"todo_group_test_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var settingsService = new FakeSettingsService();
            var mainVm = new MainViewModel(
                new FakeMediaSessionService(),
                new FakeLyricsService(),
                settingsService,
                todoService,
                new GoogleDriveSyncService(settingsService));

            var item1 = new TodoItemViewModel(new TodoItem { Title = "Beli kopi", Category = "" });
            var item2 = new TodoItemViewModel(new TodoItem { Title = "Review PR", Category = "Kerja" });

            mainVm.Todos.Add(item1);
            mainVm.Todos.Add(item2);

            Assert.Equal("Tanpa Kategori", item1.CategoryGroup);
            Assert.Equal("Kerja", item2.CategoryGroup);

            // Move item1 (no category) to item2 (Kerja)
            mainVm.MoveTodo(item1, item2);

            Assert.Equal("Kerja", item1.Category);
            Assert.Equal("Kerja", item1.CategoryGroup);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
    [Fact]
    public void MainViewModel_MoveCategoryUp_And_Down_MovesAllChildrenWithCategory()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cat_move_test_{Guid.NewGuid():N}.json");
        try
        {
            var todoService = new TodoService(tempFile);
            var settingsService = new FakeSettingsService();
            var mainVm = new MainViewModel(
                new FakeMediaSessionService(),
                new FakeLyricsService(),
                settingsService,
                todoService,
                new GoogleDriveSyncService(settingsService));

            // Setup categories: Kerja (2 items), Pribadi (2 items), Belanja (1 item)
            var k1 = new TodoItemViewModel(new TodoItem { Title = "Review PR", Category = "Kerja" });
            var k2 = new TodoItemViewModel(new TodoItem { Title = "Deploy prod", Category = "Kerja" });
            var p1 = new TodoItemViewModel(new TodoItem { Title = "Olahraga", Category = "Pribadi" });
            var p2 = new TodoItemViewModel(new TodoItem { Title = "Baca buku", Category = "Pribadi" });
            var b1 = new TodoItemViewModel(new TodoItem { Title = "Beli susu", Category = "Belanja" });

            mainVm.Todos.Add(k1);
            mainVm.Todos.Add(k2);
            mainVm.Todos.Add(p1);
            mainVm.Todos.Add(p2);
            mainVm.Todos.Add(b1);

            var initialOrder = mainVm.GetCategoryOrder();
            Assert.Equal(new[] { "Kerja", "Pribadi", "Belanja" }, initialOrder);

            // Move "Pribadi" UP -> should become: Pribadi, Kerja, Belanja
            mainVm.MoveCategoryUpCommand.Execute("Pribadi");

            var orderAfterUp = mainVm.GetCategoryOrder();
            Assert.Equal(new[] { "Pribadi", "Kerja", "Belanja" }, orderAfterUp);

            // Children of Pribadi (p1, p2) must be first, followed by Kerja (k1, k2), then Belanja (b1)
            Assert.Equal(new[] { p1, p2, k1, k2, b1 }, mainVm.Todos.ToArray());

            // Move "Pribadi" DOWN -> should swap back: Kerja, Pribadi, Belanja
            mainVm.MoveCategoryDownCommand.Execute("Pribadi");

            var orderAfterDown = mainVm.GetCategoryOrder();
            Assert.Equal(new[] { "Kerja", "Pribadi", "Belanja" }, orderAfterDown);
            Assert.Equal(new[] { k1, k2, p1, p2, b1 }, mainVm.Todos.ToArray());

            // Move "Belanja" to top via Drag & Drop MoveCategory("Belanja", "Kerja")
            mainVm.MoveCategory("Belanja", "Kerja");
            var orderAfterDrag = mainVm.GetCategoryOrder();
            Assert.Equal(new[] { "Belanja", "Kerja", "Pribadi" }, orderAfterDrag);
            Assert.Equal(new[] { b1, k1, k2, p1, p2 }, mainVm.Todos.ToArray());

            // Moving the top category UP or bottom category DOWN is a safe no-op
            mainVm.MoveCategoryUpCommand.Execute("Belanja");
            Assert.Equal(new[] { "Belanja", "Kerja", "Pribadi" }, mainVm.GetCategoryOrder());
            mainVm.MoveCategoryDownCommand.Execute("Pribadi");
            Assert.Equal(new[] { "Belanja", "Kerja", "Pribadi" }, mainVm.GetCategoryOrder());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
