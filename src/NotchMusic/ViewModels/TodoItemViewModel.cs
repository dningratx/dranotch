using System;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Models;

namespace NotchMusic.ViewModels;

public class TodoItemViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _category = string.Empty;
    private string _taskText = string.Empty;
    private bool _isCompleted;
    private DateTime? _completedAt;
    private DateTime? _dueDate;
    private bool _isDueNotified;
    private bool _isEditing;
    private DateTime? _editDueDate;
    private bool _isSettingEditDueDateInDetached;
    private bool _isSettingEditDueDateInMain;
    private DateTime? _editCustomDeadlineDate = DateTime.Today;
    private int _editCustomDeadlineHour = 17;
    private int _editCustomDeadlineMinute = 0;
    private string _editText = string.Empty;

    public string Id { get; }
    public DateTime CreatedAt { get; }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value);
    }

    public DateTime? EditDueDate
    {
        get => _editDueDate;
        set
        {
            if (SetProperty(ref _editDueDate, value))
            {
                OnPropertyChanged(nameof(HasEditDueDate));
                OnPropertyChanged(nameof(EditDueDateText));
            }
        }
    }

    public bool HasEditDueDate => EditDueDate.HasValue;

    public string EditDueDateText
    {
        get
        {
            if (!EditDueDate.HasValue) return string.Empty;
            var dt = EditDueDate.Value;
            var today = DateTime.Today;
            if (dt.Date == today)
                return $"Hari ini {dt:HH:mm}";
            if (dt.Date == today.AddDays(1))
                return $"Besok {dt:HH:mm}";
            if (dt.Date == today.AddDays(-1))
                return $"Kemarin {dt:HH:mm}";
            if (dt.Year == today.Year)
                return dt.ToString("d MMM HH:mm");
            return dt.ToString("d MMM yyyy HH:mm");
        }
    }

    public bool IsSettingEditDueDateInDetached
    {
        get => _isSettingEditDueDateInDetached;
        set
        {
            if (SetProperty(ref _isSettingEditDueDateInDetached, value))
            {
                if (value) _isSettingEditDueDateInMain = false;
                OnPropertyChanged(nameof(IsSettingEditDueDateInMain));
                OnPropertyChanged(nameof(IsSettingEditDueDate));
            }
        }
    }

    public bool IsSettingEditDueDateInMain
    {
        get => _isSettingEditDueDateInMain;
        set
        {
            if (SetProperty(ref _isSettingEditDueDateInMain, value))
            {
                if (value) _isSettingEditDueDateInDetached = false;
                OnPropertyChanged(nameof(IsSettingEditDueDateInDetached));
                OnPropertyChanged(nameof(IsSettingEditDueDate));
            }
        }
    }

    public bool IsSettingEditDueDate
    {
        get => _isSettingEditDueDateInDetached || _isSettingEditDueDateInMain;
        set
        {
            _isSettingEditDueDateInDetached = value;
            _isSettingEditDueDateInMain = value;
            OnPropertyChanged(nameof(IsSettingEditDueDateInDetached));
            OnPropertyChanged(nameof(IsSettingEditDueDateInMain));
            OnPropertyChanged(nameof(IsSettingEditDueDate));
        }
    }

    public DateTime? EditCustomDeadlineDate
    {
        get => _editCustomDeadlineDate;
        set => SetProperty(ref _editCustomDeadlineDate, value);
    }

    public int EditCustomDeadlineHour
    {
        get => _editCustomDeadlineHour;
        set => SetProperty(ref _editCustomDeadlineHour, value);
    }

    public int EditCustomDeadlineMinute
    {
        get => _editCustomDeadlineMinute;
        set => SetProperty(ref _editCustomDeadlineMinute, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                var (cat, text) = TodoItem.ParseCategory(value);
                _category = cat;
                _taskText = !string.IsNullOrEmpty(cat) ? text : value;
                OnPropertyChanged(nameof(Category));
                OnPropertyChanged(nameof(CategoryGroup));
                OnPropertyChanged(nameof(TaskText));
                OnPropertyChanged(nameof(HasCategory));
                OnChanged?.Invoke();
            }
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                _title = HasCategory ? $"{_category}: {_taskText}" : _taskText;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(CategoryGroup));
                OnPropertyChanged(nameof(HasCategory));
                OnChanged?.Invoke();
            }
        }
    }

    public string CategoryGroup => string.IsNullOrWhiteSpace(_category) ? "Tanpa Kategori" : _category.Trim();

    public string TaskText
    {
        get => string.IsNullOrEmpty(_taskText) ? _title : _taskText;
        set
        {
            if (SetProperty(ref _taskText, value))
            {
                _title = HasCategory ? $"{_category}: {_taskText}" : _taskText;
                OnPropertyChanged(nameof(Title));
                OnChanged?.Invoke();
            }
        }
    }

    public bool HasCategory => !string.IsNullOrWhiteSpace(_category);

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                CompletedAt = value ? DateTime.Now : null;
                RefreshDueStatus();
                OnChanged?.Invoke();
            }
        }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        private set => SetProperty(ref _completedAt, value);
    }

    public DateTime? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetProperty(ref _dueDate, value))
            {
                _isDueNotified = false; // Reset notification when deadline changes
                RefreshDueStatus();
                OnPropertyChanged(nameof(HasDueDate));
                OnChanged?.Invoke();
            }
        }
    }

    public bool IsDueNotified
    {
        get => _isDueNotified;
        set => SetProperty(ref _isDueNotified, value);
    }

    public bool HasDueDate => DueDate.HasValue;

    public bool IsOverdue => !IsCompleted && DueDate.HasValue && DueDate.Value < DateTime.Now;

    public string DueDateText
    {
        get
        {
            if (!DueDate.HasValue) return string.Empty;
            var dt = DueDate.Value;
            var today = DateTime.Today;
            if (dt.Date == today)
                return $"Hari ini {dt:HH:mm}";
            if (dt.Date == today.AddDays(1))
                return $"Besok {dt:HH:mm}";
            if (dt.Date == today.AddDays(-1))
                return $"Kemarin {dt:HH:mm}";
            if (dt.Year == today.Year)
                return dt.ToString("d MMM HH:mm");
            return dt.ToString("d MMM yyyy HH:mm");
        }
    }

    public string RemainingTimeText
    {
        get
        {
            if (!DueDate.HasValue) return string.Empty;
            if (IsCompleted) return "Selesai";

            var now = DateTime.Now;
            var due = DueDate.Value;

            if (due < now)
            {
                var diff = now - due;
                if (diff.TotalDays >= 1)
                {
                    var days = (int)diff.TotalDays;
                    var hours = diff.Hours;
                    return hours > 0 ? $"Terlewat {days} hr {hours} jam" : $"Terlewat {days} hr";
                }
                if (diff.TotalHours >= 1)
                {
                    var hours = (int)diff.TotalHours;
                    var mins = diff.Minutes;
                    return mins > 0 ? $"Terlewat {hours} jam {mins} mnt" : $"Terlewat {hours} jam";
                }
                if (diff.TotalMinutes >= 1)
                {
                    var mins = (int)diff.TotalMinutes;
                    return $"Terlewat {mins} mnt";
                }
                return "Baru saja terlewat";
            }
            else
            {
                var diff = due - now;
                if (diff.TotalDays >= 1)
                {
                    var days = (int)diff.TotalDays;
                    var hours = diff.Hours;
                    return hours > 0 ? $"Sisa {days} hr {hours} jam" : $"Sisa {days} hr";
                }
                if (diff.TotalHours >= 1)
                {
                    var hours = (int)diff.TotalHours;
                    var mins = diff.Minutes;
                    return mins > 0 ? $"Sisa {hours} jam {mins} mnt" : $"Sisa {hours} jam";
                }
                if (diff.TotalMinutes >= 1)
                {
                    var mins = (int)diff.TotalMinutes;
                    return $"Sisa {mins} mnt";
                }
                return "Sisa < 1 mnt";
            }
        }
    }

    public string DueDateBadgeText
    {
        get
        {
            if (!DueDate.HasValue) return string.Empty;
            if (IsCompleted) return $"{DueDateText} (Selesai)";
            var remaining = RemainingTimeText;
            if (string.IsNullOrEmpty(remaining)) return DueDateText;
            return $"{DueDateText} • {remaining}";
        }
    }

    public string DueDateFullTooltip
    {
        get
        {
            if (!DueDate.HasValue) return string.Empty;
            var dt = DueDate.Value;
            var remaining = !string.IsNullOrEmpty(RemainingTimeText) ? $"\nStatus: {RemainingTimeText}" : "";
            return $"Batas Waktu: {dt:dddd, d MMMM yyyy HH:mm}{remaining}";
        }
    }

    public string DueBadgeBackground
    {
        get
        {
            if (IsCompleted) return "#181820";
            if (IsOverdue) return "#3A141A";
            if (DueDate.HasValue && (DueDate.Value - DateTime.Now).TotalHours <= 2) return "#382510";
            return "#142236";
        }
    }

    public string DueBadgeBorder
    {
        get
        {
            if (IsCompleted) return "#2C2C38";
            if (IsOverdue) return "#FF4D6D";
            if (DueDate.HasValue && (DueDate.Value - DateTime.Now).TotalHours <= 2) return "#FFAA00";
            return "#3A6090";
        }
    }

    public string DueBadgeForeground
    {
        get
        {
            if (IsCompleted) return "#777788";
            if (IsOverdue) return "#FF6B8B";
            if (DueDate.HasValue && (DueDate.Value - DateTime.Now).TotalHours <= 2) return "#FFC04D";
            return "#8AB4F8";
        }
    }

    public void RefreshDueStatus()
    {
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(DueDateText));
        OnPropertyChanged(nameof(RemainingTimeText));
        OnPropertyChanged(nameof(DueDateBadgeText));
        OnPropertyChanged(nameof(DueDateFullTooltip));
        OnPropertyChanged(nameof(DueBadgeBackground));
        OnPropertyChanged(nameof(DueBadgeBorder));
        OnPropertyChanged(nameof(DueBadgeForeground));
    }

    public event Action? OnChanged;
    public event Action<TodoItemViewModel>? OnDeleteRequested;

    public ICommand DeleteCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ClearDueDateCommand { get; }
    public ICommand SetDueDatePresetCommand { get; }
    public ICommand ToggleEditDueDateCommand { get; }
    public ICommand ToggleEditDueDateDetachedCommand { get; }
    public ICommand ToggleEditDueDateMainCommand { get; }
    public ICommand CloseEditDueDatePopupCommand { get; }
    public ICommand ClearEditDueDateCommand { get; }
    public ICommand SetEditDueDatePresetCommand { get; }
    public ICommand ApplyEditCustomDeadlineCommand { get; }

    public void CloseEditDueDatePopup()
    {
        IsSettingEditDueDateInDetached = false;
        IsSettingEditDueDateInMain = false;
    }

    public void ApplyEditCustomDeadline()
    {
        var baseDate = EditCustomDeadlineDate ?? DateTime.Today;
        int hour = Math.Clamp(EditCustomDeadlineHour, 0, 23);
        int minute = Math.Clamp(EditCustomDeadlineMinute, 0, 59);
        EditDueDate = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, 0);
        CloseEditDueDatePopup();
    }

    public void SetDueDatePreset(string preset)
    {
        var now = DateTime.Now;
        DateTime target;
        switch (preset?.ToLowerInvariant())
        {
            case "plus_1h":
                target = now.AddHours(1);
                DueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "plus_3h":
                target = now.AddHours(3);
                DueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "today_17":
                DueDate = new DateTime(now.Year, now.Month, now.Day, 17, 0, 0);
                break;
            case "today_21":
                DueDate = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);
                break;
            case "tomorrow_09":
                target = now.AddDays(1);
                DueDate = new DateTime(target.Year, target.Month, target.Day, 9, 0, 0);
                break;
            case "tomorrow_17":
                target = now.AddDays(1);
                DueDate = new DateTime(target.Year, target.Month, target.Day, 17, 0, 0);
                break;
            default:
                break;
        }
    }

    public void SetEditDueDatePreset(string preset)
    {
        var now = DateTime.Now;
        DateTime target;
        switch (preset?.ToLowerInvariant())
        {
            case "plus_1h":
                target = now.AddHours(1);
                EditDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "plus_3h":
                target = now.AddHours(3);
                EditDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "today_17":
                EditDueDate = new DateTime(now.Year, now.Month, now.Day, 17, 0, 0);
                break;
            case "today_21":
                EditDueDate = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);
                break;
            case "tomorrow_09":
                target = now.AddDays(1);
                EditDueDate = new DateTime(target.Year, target.Month, target.Day, 9, 0, 0);
                break;
            case "tomorrow_17":
                target = now.AddDays(1);
                EditDueDate = new DateTime(target.Year, target.Month, target.Day, 17, 0, 0);
                break;
            default:
                break;
        }

        if (EditDueDate.HasValue)
        {
            EditCustomDeadlineDate = EditDueDate.Value.Date;
            EditCustomDeadlineHour = EditDueDate.Value.Hour;
            EditCustomDeadlineMinute = EditDueDate.Value.Minute;
        }
        CloseEditDueDatePopup();
    }

    public void StartEdit()
    {
        EditText = Title;
        EditDueDate = DueDate;
        EditCustomDeadlineDate = DueDate?.Date ?? DateTime.Today;
        EditCustomDeadlineHour = DueDate?.Hour ?? 17;
        EditCustomDeadlineMinute = DueDate?.Minute ?? 0;
        CloseEditDueDatePopup();
        IsEditing = true;
    }

    public void SaveEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditText))
        {
            Title = EditText.Trim();
        }
        DueDate = EditDueDate;
        CloseEditDueDatePopup();
        IsEditing = false;
    }

    public void CancelEdit()
    {
        EditText = Title;
        EditDueDate = DueDate;
        CloseEditDueDatePopup();
        IsEditing = false;
    }

    public TodoItemViewModel(TodoItem item)
    {
        Id = item.Id;
        _isCompleted = item.IsCompleted;
        CreatedAt = item.CreatedAt;
        _completedAt = item.CompletedAt;
        _dueDate = item.DueDate;
        _isDueNotified = item.IsDueNotified;

        var rawTitle = item.Title ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            _category = item.Category.Trim();
            var (_, parsedText) = TodoItem.ParseCategory(rawTitle);
            _taskText = !string.IsNullOrWhiteSpace(parsedText) ? parsedText : rawTitle;
            _title = rawTitle.Contains(':') ? rawTitle : $"{_category}: {_taskText}";
        }
        else
        {
            var (parsedCat, parsedText) = TodoItem.ParseCategory(rawTitle);
            _category = parsedCat;
            _taskText = !string.IsNullOrWhiteSpace(parsedCat) ? parsedText : rawTitle;
            _title = rawTitle;
        }

        DeleteCommand = new RelayCommand(() => OnDeleteRequested?.Invoke(this));
        StartEditCommand = new RelayCommand(StartEdit);
        SaveEditCommand = new RelayCommand(SaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        ClearDueDateCommand = new RelayCommand(() => DueDate = null);
        SetDueDatePresetCommand = new RelayCommand((p) =>
        {
            if (p is string presetStr)
            {
                SetDueDatePreset(presetStr);
            }
        });
        ToggleEditDueDateCommand = new RelayCommand(() => IsSettingEditDueDate = !IsSettingEditDueDate);
        ToggleEditDueDateDetachedCommand = new RelayCommand(() => IsSettingEditDueDateInDetached = !IsSettingEditDueDateInDetached);
        ToggleEditDueDateMainCommand = new RelayCommand(() => IsSettingEditDueDateInMain = !IsSettingEditDueDateInMain);
        CloseEditDueDatePopupCommand = new RelayCommand(CloseEditDueDatePopup);
        ClearEditDueDateCommand = new RelayCommand(() =>
        {
            EditDueDate = null;
            CloseEditDueDatePopup();
        });
        SetEditDueDatePresetCommand = new RelayCommand((p) =>
        {
            if (p is string presetStr)
            {
                SetEditDueDatePreset(presetStr);
            }
        });
        ApplyEditCustomDeadlineCommand = new RelayCommand(ApplyEditCustomDeadline);
    }

    public TodoItem ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Category = Category,
        IsCompleted = IsCompleted,
        CreatedAt = CreatedAt,
        CompletedAt = CompletedAt,
        DueDate = DueDate,
        IsDueNotified = IsDueNotified
    };
}
