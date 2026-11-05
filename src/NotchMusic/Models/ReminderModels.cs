using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NotchMusic.ViewModels;

namespace NotchMusic.Models;

public enum ReminderType
{
    Interval = 0,
    DailyTime = 1
}

public class ReminderItem : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private string _category = "Kesehatan";
    private string _icon = "💧";
    private string _notes = string.Empty;
    private ReminderType _type = ReminderType.Interval;
    private int _intervalMinutes = 60;
    private List<string> _dailyTimes = new() { "08:00" };
    private bool _isEnabled = true;
    private DateTime? _nextTriggerTime;
    private DateTime? _lastTriggeredAt;
    private DateTime? _snoozedUntil;
    private int _completedTodayCount;
    private DateTime _lastCompletedDate = DateTime.Today;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(ScheduleSummaryText));
            }
        }
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Icon
    {
        get => string.IsNullOrWhiteSpace(_icon) ? "⏰" : _icon;
        set
        {
            if (SetProperty(ref _icon, value))
            {
                OnPropertyChanged(nameof(IconGeometry));
                OnPropertyChanged(nameof(AccentColor));
                OnPropertyChanged(nameof(BadgeBackground));
                OnPropertyChanged(nameof(BadgeBorder));
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetProperty(ref _notes, value))
            {
                OnPropertyChanged(nameof(HasNotes));
            }
        }
    }

    [JsonIgnore]
    public bool HasNotes => !string.IsNullOrWhiteSpace(_notes);

    public ReminderType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(IsIntervalType));
                OnPropertyChanged(nameof(IsDailyTimeType));
                OnPropertyChanged(nameof(TypeBadgeText));
                OnPropertyChanged(nameof(ScheduleSummaryText));
            }
        }
    }

    [JsonIgnore]
    public bool IsIntervalType => _type == ReminderType.Interval;

    [JsonIgnore]
    public bool IsDailyTimeType => _type == ReminderType.DailyTime;

    public int IntervalMinutes
    {
        get => _intervalMinutes;
        set
        {
            int clamped = Math.Clamp(value, 1, 1440);
            if (SetProperty(ref _intervalMinutes, clamped))
            {
                OnPropertyChanged(nameof(ScheduleSummaryText));
            }
        }
    }

    public List<string> DailyTimes
    {
        get => _dailyTimes;
        set
        {
            _dailyTimes = value ?? new List<string>();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DailyTimesFormatted));
            OnPropertyChanged(nameof(ScheduleSummaryText));
        }
    }

    [JsonIgnore]
    public string DailyTimesFormatted => _dailyTimes != null && _dailyTimes.Count > 0
        ? string.Join(", ", _dailyTimes.OrderBy(t => t))
        : "08:00";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(CountdownText));
                OnPropertyChanged(nameof(NextTriggerFormatted));
            }
        }
    }

    public DateTime? NextTriggerTime
    {
        get => _nextTriggerTime;
        set
        {
            if (SetProperty(ref _nextTriggerTime, value))
            {
                OnPropertyChanged(nameof(CountdownText));
                OnPropertyChanged(nameof(NextTriggerFormatted));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }

    public DateTime? LastTriggeredAt
    {
        get => _lastTriggeredAt;
        set => SetProperty(ref _lastTriggeredAt, value);
    }

    public DateTime? SnoozedUntil
    {
        get => _snoozedUntil;
        set
        {
            if (SetProperty(ref _snoozedUntil, value))
            {
                OnPropertyChanged(nameof(IsSnoozed));
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(CountdownText));
            }
        }
    }

    [JsonIgnore]
    public bool IsSnoozed => _snoozedUntil.HasValue && _snoozedUntil.Value > DateTime.Now;

    public int CompletedTodayCount
    {
        get => _completedTodayCount;
        set
        {
            if (SetProperty(ref _completedTodayCount, value))
            {
                OnPropertyChanged(nameof(HasCompletedToday));
                OnPropertyChanged(nameof(CompletedTodayBadgeText));
            }
        }
    }

    public DateTime LastCompletedDate
    {
        get => _lastCompletedDate;
        set => SetProperty(ref _lastCompletedDate, value);
    }

    [JsonIgnore]
    public bool HasCompletedToday => _completedTodayCount > 0 && _lastCompletedDate.Date == DateTime.Today;

    [JsonIgnore]
    public string CompletedTodayBadgeText => $"✓ {_completedTodayCount}x hari ini";

    [JsonIgnore]
    public string TypeBadgeText => _type == ReminderType.Interval ? "INTERVAL" : "HARIAN";

    [JsonIgnore]
    public string ScheduleSummaryText
    {
        get
        {
            if (_type == ReminderType.Interval)
            {
                if (_intervalMinutes >= 60 && _intervalMinutes % 60 == 0)
                {
                    int hrs = _intervalMinutes / 60;
                    return $"Setiap {hrs} jam sekali";
                }
                if (_intervalMinutes > 60)
                {
                    int hrs = _intervalMinutes / 60;
                    int mins = _intervalMinutes % 60;
                    return $"Setiap {hrs} jam {mins} menit";
                }
                return $"Setiap {_intervalMinutes} menit sekali";
            }

            return $"Setiap hari pukul {DailyTimesFormatted}";
        }
    }

    [JsonIgnore]
    public string NextTriggerFormatted
    {
        get
        {
            if (!IsEnabled) return "Nonaktif";
            var target = EffectiveNextTriggerTime;
            if (!target.HasValue) return "-";

            if (target.Value.Date == DateTime.Today)
                return target.Value.ToString("HH:mm");
            if (target.Value.Date == DateTime.Today.AddDays(1))
                return $"Besok {target.Value:HH:mm}";
            return target.Value.ToString("dd/MM HH:mm");
        }
    }

    [JsonIgnore]
    public DateTime? EffectiveNextTriggerTime
    {
        get
        {
            if (!IsEnabled) return null;
            if (_snoozedUntil.HasValue && _snoozedUntil.Value > DateTime.Now)
                return _snoozedUntil.Value;
            return _nextTriggerTime;
        }
    }

    [JsonIgnore]
    public string CountdownText
    {
        get
        {
            if (!IsEnabled) return "OFF";
            var target = EffectiveNextTriggerTime;
            if (!target.HasValue) return "-";

            var diff = target.Value - DateTime.Now;
            if (diff.TotalSeconds <= 0)
                return "Sekarang!";

            if (diff.TotalHours >= 1)
            {
                int h = (int)diff.TotalHours;
                int m = diff.Minutes;
                return m > 0 ? $"{h}j {m}m" : $"{h} jam";
            }

            if (diff.TotalMinutes >= 1)
            {
                int m = (int)Math.Ceiling(diff.TotalMinutes);
                return $"{m} mnt";
            }

            int s = Math.Max(1, (int)Math.Ceiling(diff.TotalSeconds));
            return $"{s} dtk";
        }
    }

    [JsonIgnore]
    public string StatusBadgeText => !IsEnabled ? "OFF" : (IsSnoozed ? "TUNDA" : CountdownText);

    // Vector Path Geometries (24x24) agar ikon pengingat terlihat tajam, jelas, dan berwarna terang di WPF
    public const string WaterDropGeometry = "M 12,2 C 12,2 5,10.2 5,15 C 5,18.87 8.13,22 12,22 C 15.87,22 19,18.87 19,15 C 19,10.2 12,2 12,2 Z M 8.5,14.5 C 8.5,12.8 9.8,10.6 10.8,9.2 C 10.3,11.2 10.2,13.0 10.8,14.8 C 11.3,16.2 12.6,17.2 14.2,17.5 C 13.5,18.2 12.5,18.5 11.5,18.5 C 9.8,18.5 8.5,16.8 8.5,14.5 Z";
    public const string MedicinePillGeometry = "M 19.07,4.93 C 16.92,2.78 13.44,2.78 11.29,4.93 L 4.93,11.29 C 2.78,13.44 2.78,16.92 4.93,19.07 C 7.08,21.22 10.56,21.22 12.71,19.07 L 19.07,12.71 C 21.22,10.56 21.22,7.08 19.07,4.93 Z M 17.45,11.09 L 14.45,14.09 L 9.91,9.55 L 12.91,6.55 C 14.16,5.30 16.20,5.30 17.45,6.55 C 18.70,7.80 18.70,9.84 17.45,11.09 Z";
    public const string EyeRestGeometry = "M 12,4.5 C 7,4.5 2.73,7.61 1,12 C 2.73,16.39 7,19.5 12,19.5 C 17,19.5 21.27,16.39 23,12 C 21.27,7.61 17,4.5 12,4.5 Z M 12,17 C 9.24,17 7,14.76 7,12 C 7,9.24 9.24,7 12,7 C 14.76,7 17,9.24 17,12 C 17,14.76 14.76,17 12,17 Z M 12,9 C 10.34,9 9,10.34 9,12 C 9,13.66 10.34,15 12,15 C 13.66,15 15,13.66 15,12 C 15,10.34 13.66,9 12,9 Z";
    public const string StretchBodyGeometry = "M 12,2 C 13.1,2 14,2.9 14,4 C 14,5.1 13.1,6 12,6 C 10.9,6 10,5.1 10,4 C 10,2.9 10.9,2 12,2 Z M 21,9 H 15 V 22 H 13 V 16 H 11 V 22 H 9 V 9 H 3 V 7 H 21 V 9 Z";
    public const string UtensilsMealGeometry = "M 11,9 H 9 V 2 H 7 V 9 H 5 V 2 H 3 V 9 C 3,11.12 4.66,12.84 6.75,12.97 V 22 H 9.25 V 12.97 C 11.34,12.84 13,11.12 13,9 V 2 H 11 V 9 Z M 16,6 V 14 H 18.5 V 22 H 21 V 2 C 18.24,2 16,4.24 16,6 Z";
    public const string CoffeeCupGeometry = "M 20,3 H 4 V 13 C 4,15.21 5.79,17 8,17 H 14 C 16.21,17 18,15.21 18,13 V 10 H 20 C 21.11,10 22,9.11 22,8 V 5 C 22,3.89 21.11,3 20,3 Z M 20,8 H 18 V 5 H 20 V 8 Z M 4,19 H 20 V 21 H 4 V 19 Z";
    public const string WorkoutDumbbellGeometry = "M 20.57,14.86 L 22,13.43 L 20.57,12 L 17,15.57 L 8.43,7 L 12,3.43 L 10.57,2 L 9.14,3.43 L 7.71,2 L 5.57,4.14 L 4.14,2.71 L 2.71,4.14 L 4.14,5.57 L 2,7.71 L 3.43,9.14 L 2,10.57 L 3.43,12 L 7,8.43 L 15.57,17 L 12,20.57 L 13.43,22 L 14.86,20.57 L 16.29,22 L 18.43,19.86 L 19.86,21.29 L 21.29,19.86 L 19.86,18.43 L 22,16.29 L 20.57,14.86 Z";
    public const string AlarmBellGeometry = "M 12,22 C 13.1,22 14,21.1 14,20 H 10 C 10,21.1 10.9,22 12,22 Z M 18,16 V 11 C 18,7.93 16.37,5.36 13.5,4.68 V 4 C 13.5,3.17 12.83,2.5 12,2.5 C 11.17,2.5 10.5,3.17 10.5,4 V 4.68 C 7.64,5.36 6,7.92 6,11 V 16 L 4,18 V 19 H 20 V 18 L 18,16 Z";

    public static string GetIconGeometryForKey(string? icon) => icon switch
    {
        "💧" => WaterDropGeometry,
        "💊" => MedicinePillGeometry,
        "👀" => EyeRestGeometry,
        "🧘" => StretchBodyGeometry,
        "🍽️" => UtensilsMealGeometry,
        "☕" => CoffeeCupGeometry,
        "🏃" => WorkoutDumbbellGeometry,
        _ => AlarmBellGeometry
    };

    public static string GetAccentColorForKey(string? icon) => icon switch
    {
        "💧" => "#38BDF8", // Sky Blue (Minum Air)
        "💊" => "#FB7185", // Bright Rose Red (Minum Obat)
        "👀" => "#C084FC", // Bright Purple (Istirahat Mata)
        "🧘" => "#34D399", // Bright Emerald Green (Peregangan)
        "🍽️" => "#FBBF24", // Bright Amber (Makan)
        "☕" => "#F59E0B", // Warm Gold (Kopi/Teh)
        "🏃" => "#2DD4BF", // Teal (Olahraga)
        _ => "#60A5FA"     // Default Bright Blue (Alarm)
    };

    public static string GetBadgeBackgroundForKey(string? icon) => icon switch
    {
        "💧" => "#102A3E",
        "💊" => "#3A1522",
        "👀" => "#2A1842",
        "🧘" => "#102E24",
        "🍽️" => "#33260E",
        "☕" => "#33220E",
        "🏃" => "#102E2C",
        _ => "#182438"
    };

    public static string GetBadgeBorderForKey(string? icon) => icon switch
    {
        "💧" => "#286494",
        "💊" => "#7A2844",
        "👀" => "#5C368C",
        "🧘" => "#226850",
        "🍽️" => "#70521C",
        "☕" => "#704A1C",
        "🏃" => "#226862",
        _ => "#34507C"
    };

    [JsonIgnore]
    public string IconGeometry => GetIconGeometryForKey(Icon);

    [JsonIgnore]
    public string AccentColor => GetAccentColorForKey(Icon);

    [JsonIgnore]
    public string BadgeBackground => GetBadgeBackgroundForKey(Icon);

    [JsonIgnore]
    public string BadgeBorder => GetBadgeBorderForKey(Icon);

    public void RefreshDynamicProperties()
    {
        if (_lastCompletedDate.Date != DateTime.Today)
        {
            _completedTodayCount = 0;
            _lastCompletedDate = DateTime.Today;
            OnPropertyChanged(nameof(CompletedTodayCount));
            OnPropertyChanged(nameof(HasCompletedToday));
            OnPropertyChanged(nameof(CompletedTodayBadgeText));
        }

        OnPropertyChanged(nameof(IsSnoozed));
        OnPropertyChanged(nameof(EffectiveNextTriggerTime));
        OnPropertyChanged(nameof(NextTriggerFormatted));
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(StatusBadgeText));
    }

    public void CalculateNextTrigger(DateTime fromTime, bool forceAdvance = false)
    {
        if (!IsEnabled)
        {
            NextTriggerTime = null;
            return;
        }

        if (Type == ReminderType.Interval)
        {
            int mins = Math.Max(1, IntervalMinutes);
            if (!forceAdvance && NextTriggerTime.HasValue && NextTriggerTime.Value > fromTime)
            {
                return;
            }
            NextTriggerTime = fromTime.AddMinutes(mins);
        }
        else
        {
            var validTimes = new List<TimeSpan>();
            if (DailyTimes != null)
            {
                foreach (var tStr in DailyTimes)
                {
                    if (TimeSpan.TryParse(tStr, out var ts))
                    {
                        validTimes.Add(ts);
                    }
                }
            }

            if (validTimes.Count == 0)
            {
                validTimes.Add(new TimeSpan(8, 0, 0));
            }

            validTimes.Sort();

            var today = fromTime.Date;
            foreach (var ts in validTimes)
            {
                var candidate = today + ts;
                if (candidate > fromTime.AddSeconds(2))
                {
                    NextTriggerTime = candidate;
                    return;
                }
            }

            // Next day first slot
            NextTriggerTime = today.AddDays(1) + validTimes[0];
        }
    }
}

public class ReminderPresetOption
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = "💧";
    public string Notes { get; set; } = string.Empty;
    public ReminderType Type { get; set; } = ReminderType.Interval;
    public int IntervalMinutes { get; set; } = 60;
    public List<string> DailyTimes { get; set; } = new();
    public string Subtitle { get; set; } = string.Empty;
}

public class ReminderNotificationEvent
{
    public string ReminderId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "💧";
    public string IconGeometry => ReminderItem.GetIconGeometryForKey(Icon);
    public string BadgeBackground => ReminderItem.GetBadgeBackgroundForKey(Icon);
    public string BadgeBorder => ReminderItem.GetBadgeBorderForKey(Icon);
    public string Category { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#38BDF8";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ReminderIconOption
{
    public string Key { get; set; } = "💧";
    public string Label { get; set; } = "Air";
    public string IconGeometry => ReminderItem.GetIconGeometryForKey(Key);
    public string AccentColor => ReminderItem.GetAccentColorForKey(Key);
    public string BadgeBackground => ReminderItem.GetBadgeBackgroundForKey(Key);
    public string BadgeBorder => ReminderItem.GetBadgeBorderForKey(Key);
}

public class AlarmSoundOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔔";
    public string FileName { get; set; } = string.Empty;

    public string DisplayTitle => $"{Icon} {Name}";
}
