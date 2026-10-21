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

    public string Id { get; }
    public DateTime CreatedAt { get; }

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
                OnPropertyChanged(nameof(HasCategory));
                OnChanged?.Invoke();
            }
        }
    }

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
                OnChanged?.Invoke();
            }
        }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        private set => SetProperty(ref _completedAt, value);
    }

    public event Action? OnChanged;
    public event Action<TodoItemViewModel>? OnDeleteRequested;

    public ICommand DeleteCommand { get; }

    public TodoItemViewModel(TodoItem item)
    {
        Id = item.Id;
        _isCompleted = item.IsCompleted;
        CreatedAt = item.CreatedAt;
        _completedAt = item.CompletedAt;

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
    }

    public TodoItem ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Category = Category,
        IsCompleted = IsCompleted,
        CreatedAt = CreatedAt,
        CompletedAt = CompletedAt
    };
}
