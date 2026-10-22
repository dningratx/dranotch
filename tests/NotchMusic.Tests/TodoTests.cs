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
            Assert.Equal("Beli Susu", mainVm.CurrentTickerText);
            Assert.Equal("1/3", mainVm.CurrentTickerBadge);

            // Rotate Next
            mainVm.NextTickerCommand.Execute(null);
            Assert.Equal("Meeting Project", mainVm.CurrentTickerText);
            Assert.Equal("2/3", mainVm.CurrentTickerBadge);

            mainVm.NextTickerCommand.Execute(null);
            Assert.Equal("Olahraga", mainVm.CurrentTickerText);
            Assert.Equal("3/3", mainVm.CurrentTickerBadge);

            // Wrap around
            mainVm.NextTickerCommand.Execute(null);
            Assert.Equal("Beli Susu", mainVm.CurrentTickerText);
            Assert.Equal("1/3", mainVm.CurrentTickerBadge);

            // Rotate Previous
            mainVm.PreviousTickerCommand.Execute(null);
            Assert.Equal("Olahraga", mainVm.CurrentTickerText);
            Assert.Equal("3/3", mainVm.CurrentTickerBadge);

            // Complete current item ("Olahraga")
            mainVm.CompleteCurrentTickerCommand.Execute(null);
            Assert.Equal(2, mainVm.PendingTodosCount);
            Assert.Equal(1, mainVm.CompletedTodosCount);

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

    private class FakeGoogleDriveSyncService : IGoogleDriveSyncService
    {
        public bool IsConfigured { get; set; } = true;
        public bool IsConnected { get; set; } = true;
        public bool IsSyncing { get; set; } = false;
        public string UserEmail { get; set; } = "user@gmail.com";
        public DateTime? LastSyncTime { get; set; } = DateTime.Now;
        public string SyncStatus { get; set; } = "Tersambung";

        public event Action? StatusChanged;
        public event Action<List<TodoItem>>? TodosSynced;

        public Task<bool> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<bool> UploadTodosAsync(IEnumerable<TodoItem> todos, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<TodoItem>?> DownloadTodosAsync(CancellationToken ct = default) => Task.FromResult<List<TodoItem>?>(new List<TodoItem>());
        public Task<List<TodoItem>?> SyncAsync(IEnumerable<TodoItem> localTodos, CancellationToken ct = default)
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

            // Verify Ticker displays TaskText instead of repeating Category
            mainVm.UpdateTicker();
            Assert.NotNull(mainVm.CurrentTickerItem);
            Assert.True(mainVm.CurrentTickerItem!.HasCategory);
            Assert.Equal("MARIBON", mainVm.CurrentTickerItem.Category);
            Assert.Equal("twibbon hari santri", mainVm.CurrentTickerText);
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
        Assert.Equal(760, mainVm.ExpandedPanelWidth);

        // Detach Todo
        mainVm.DetachTodoCommand.Execute(null);
        Assert.True(mainVm.IsTodoDetached);
        Assert.False(mainVm.ShowTodoTicker);
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowSplitter);
        Assert.Equal(440, mainVm.ExpandedPanelWidth);

        // Re-attach Todo
        mainVm.AttachTodoCommand.Execute(null);
        Assert.False(mainVm.IsTodoDetached);
        Assert.True(mainVm.ShowTodoTicker);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.True(mainVm.ShowSplitter);
        Assert.Equal(760, mainVm.ExpandedPanelWidth);

        // Music Only
        mainVm.ComponentDisplayMode = "MusicOnly";
        Assert.True(mainVm.ShowMusicSection);
        Assert.False(mainVm.ShowTodoSection);
        Assert.False(mainVm.ShowTodoTicker);
        Assert.False(mainVm.ShowTodoSectionInMainWindow);
        Assert.Equal(440, mainVm.ExpandedPanelWidth);

        // Todo Only
        mainVm.ComponentDisplayMode = "TodoOnly";
        Assert.False(mainVm.ShowMusicSection);
        Assert.True(mainVm.ShowTodoSection);
        Assert.True(mainVm.ShowTodoTicker);
        Assert.True(mainVm.ShowTodoSectionInMainWindow);
        Assert.False(mainVm.ShowSplitter);
        Assert.Equal(440, mainVm.ExpandedPanelWidth);
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
}
