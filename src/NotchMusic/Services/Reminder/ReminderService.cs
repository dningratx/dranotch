using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Reminder;

public class ReminderService : IReminderService
{
    private readonly ISettingsService _settingsService;
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<ReminderItem> _reminders = new();
    private readonly DispatcherTimer _timer;
    private bool _isDisposed;

    public event Action<ReminderNotificationEvent>? ReminderTriggered;
    public event Action? RemindersUpdated;

    public IReadOnlyList<ReminderItem> Reminders => _reminders;

    public IReadOnlyList<ReminderPresetOption> Presets { get; } = new List<ReminderPresetOption>
    {
        new()
        {
            Id = "water_60",
            Title = "Minum Air Putih (1 Gelas)",
            Category = "Hidrasi",
            Icon = "💧",
            Notes = "Jaga tubuh tetap terhidrasi! Minum 1 gelas (~250 ml) air putih sekarang.",
            Type = ReminderType.Interval,
            IntervalMinutes = 60,
            Subtitle = "Setiap 1 Jam"
        },
        new()
        {
            Id = "medicine_3x",
            Title = "Minum Obat / Vitamin",
            Category = "Kesehatan",
            Icon = "💊",
            Notes = "Waktunya minum obat atau suplemen harian Anda sesuai dosis.",
            Type = ReminderType.DailyTime,
            DailyTimes = new List<string> { "08:00", "13:00", "20:00" },
            Subtitle = "08:00, 13:00, 20:00"
        },
        new()
        {
            Id = "eyes_20",
            Title = "Istirahat Mata (20-20-20)",
            Category = "Ergonomi",
            Icon = "👀",
            Notes = "Alihkan pandangan dari layar ke objek berjarak 20 kaki (6 meter) selama 20 detik.",
            Type = ReminderType.Interval,
            IntervalMinutes = 20,
            Subtitle = "Setiap 20 Menit"
        },
        new()
        {
            Id = "stretch_45",
            Title = "Peregangan & Berdiri Sejenak",
            Category = "Kebugaran",
            Icon = "🧘",
            Notes = "Berdiri, luruskan punggung, dan lakukan peregangan ringan selama 1-2 menit.",
            Type = ReminderType.Interval,
            IntervalMinutes = 45,
            Subtitle = "Setiap 45 Menit"
        }
    };

    public ReminderItem? NextUpcomingReminder
    {
        get
        {
            return _reminders
                .Where(r => r.IsEnabled && r.EffectiveNextTriggerTime.HasValue)
                .OrderBy(r => r.EffectiveNextTriggerTime!.Value)
                .FirstOrDefault();
        }
    }

    public ReminderService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        var appDataDir = _settingsService.GetAppDataPath();
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "reminders.json");

        Load();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
    }

    public void Start()
    {
        var now = DateTime.Now;
        foreach (var r in _reminders)
        {
            if (r.IsEnabled && (!r.NextTriggerTime.HasValue || (r.Type == ReminderType.DailyTime && r.NextTriggerTime.Value < now.AddMinutes(-1))))
            {
                r.CalculateNextTrigger(now, forceAdvance: false);
            }
            r.RefreshDynamicProperties();
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        RemindersUpdated?.Invoke();
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            _reminders.Clear();
            bool loadedFromFile = false;

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                    if (items != null)
                    {
                        _reminders.AddRange(items);
                        loadedFromFile = true;
                    }
                }
            }
            catch
            {
                // Ignore corrupted file and create default presets
            }

            if (!loadedFromFile)
            {
                SeedDefaultReminders();
                SaveInternal();
            }

            var now = DateTime.Now;
            foreach (var r in _reminders)
            {
                if (r.IsEnabled)
                {
                    if (!r.NextTriggerTime.HasValue || r.NextTriggerTime.Value <= now)
                    {
                        r.CalculateNextTrigger(now, forceAdvance: true);
                    }
                }
                r.RefreshDynamicProperties();
            }
        }
    }

    private void SeedDefaultReminders()
    {
        var now = DateTime.Now;

        var water = new ReminderItem
        {
            Title = "Minum Air Putih (1 Gelas)",
            Category = "Hidrasi",
            Icon = "💧",
            Notes = "Jaga tubuh tetap segar & fokus! Minum 1 gelas air putih sekarang.",
            Type = ReminderType.Interval,
            IntervalMinutes = 60,
            IsEnabled = true
        };
        water.CalculateNextTrigger(now, forceAdvance: true);

        var medicine = new ReminderItem
        {
            Title = "Minum Obat / Vitamin",
            Category = "Kesehatan",
            Icon = "💊",
            Notes = "Jangan lupa minum obat atau vitamin sesuai jadwal harian Anda.",
            Type = ReminderType.DailyTime,
            DailyTimes = new List<string> { "08:00", "13:00", "20:00" },
            IsEnabled = true
        };
        medicine.CalculateNextTrigger(now, forceAdvance: false);

        var eyeRest = new ReminderItem
        {
            Title = "Istirahat Mata 20-20-20",
            Category = "Ergonomi",
            Icon = "👀",
            Notes = "Lihat objek jauh (6 meter) selama 20 detik agar mata tidak lelah.",
            Type = ReminderType.Interval,
            IntervalMinutes = 30,
            IsEnabled = false
        };

        _reminders.Add(water);
        _reminders.Add(medicine);
        _reminders.Add(eyeRest);
    }

    public void Save()
    {
        lock (_lock)
        {
            SaveInternal();
        }
        RemindersUpdated?.Invoke();
    }

    private void SaveInternal()
    {
        try
        {
            var json = JsonSerializer.Serialize(_reminders, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch { }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_settingsService.CurrentSettings.EnableReminders)
            return;

        var now = DateTime.Now;
        bool stateChanged = false;

        foreach (var item in _reminders.ToList())
        {
            item.RefreshDynamicProperties();

            if (!item.IsEnabled)
                continue;

            var target = item.EffectiveNextTriggerTime;
            if (!target.HasValue)
            {
                item.CalculateNextTrigger(now, forceAdvance: true);
                stateChanged = true;
                continue;
            }

            if (now >= target.Value)
            {
                item.LastTriggeredAt = now;
                item.SnoozedUntil = null;
                item.CalculateNextTrigger(now, forceAdvance: true);
                stateChanged = true;

                FireNotification(item, now);
            }
        }

        if (stateChanged)
        {
            SaveInternal();
        }

        RemindersUpdated?.Invoke();
    }

    private void FireNotification(ReminderItem item, DateTime now)
    {
        var settings = _settingsService.CurrentSettings;
        if (settings.ReminderSoundAlert)
        {
            PrayerAudioHelper.PlayDeadlineAlert();
        }

        string msg = !string.IsNullOrWhiteSpace(item.Notes)
            ? item.Notes
            : $"Jadwal: {item.ScheduleSummaryText} — Jangan lewatkan pengingat ini!";

        var evt = new ReminderNotificationEvent
        {
            ReminderId = item.Id,
            Title = $"{item.Icon} Waktunya: {item.Title}",
            Message = msg,
            Icon = item.Icon,
            Category = item.Category,
            TimeFormatted = now.ToString("HH:mm"),
            AccentColor = item.AccentColor,
            Timestamp = now
        };

        ReminderTriggered?.Invoke(evt);
    }

    public ReminderItem AddReminder(ReminderItem item)
    {
        var now = DateTime.Now;
        item.CalculateNextTrigger(now, forceAdvance: item.Type == ReminderType.Interval);
        item.RefreshDynamicProperties();

        lock (_lock)
        {
            _reminders.Insert(0, item);
            SaveInternal();
        }

        RemindersUpdated?.Invoke();
        return item;
    }

    public ReminderItem AddFromPreset(string presetId)
    {
        var preset = Presets.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase))
                     ?? Presets[0];

        var item = new ReminderItem
        {
            Title = preset.Title,
            Category = preset.Category,
            Icon = preset.Icon,
            Notes = preset.Notes,
            Type = preset.Type,
            IntervalMinutes = preset.IntervalMinutes,
            DailyTimes = new List<string>(preset.DailyTimes),
            IsEnabled = true
        };

        return AddReminder(item);
    }

    public void UpdateReminder(ReminderItem item)
    {
        var now = DateTime.Now;
        item.SnoozedUntil = null;
        item.CalculateNextTrigger(now, forceAdvance: item.Type == ReminderType.Interval);
        item.RefreshDynamicProperties();
        Save();
    }

    public void DeleteReminder(string id)
    {
        lock (_lock)
        {
            var existing = _reminders.FirstOrDefault(r => r.Id == id);
            if (existing != null)
            {
                _reminders.Remove(existing);
                SaveInternal();
            }
        }
        RemindersUpdated?.Invoke();
    }

    public void ToggleReminder(string id)
    {
        var item = _reminders.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

        item.IsEnabled = !item.IsEnabled;
        item.SnoozedUntil = null;
        if (item.IsEnabled)
        {
            item.CalculateNextTrigger(DateTime.Now, forceAdvance: item.Type == ReminderType.Interval);
        }
        else
        {
            item.NextTriggerTime = null;
        }
        item.RefreshDynamicProperties();
        Save();
    }

    public void SnoozeReminder(string id, int minutes = 10)
    {
        var item = _reminders.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

        var now = DateTime.Now;
        item.SnoozedUntil = now.AddMinutes(Math.Max(1, minutes));
        item.IsEnabled = true;
        item.RefreshDynamicProperties();
        Save();
    }

    public void CompleteReminderOccurrence(string id)
    {
        var item = _reminders.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

        var now = DateTime.Now;
        if (item.LastCompletedDate.Date != now.Date)
        {
            item.CompletedTodayCount = 0;
            item.LastCompletedDate = now.Date;
        }

        item.CompletedTodayCount++;
        item.SnoozedUntil = null;
        if (item.IsEnabled)
        {
            item.CalculateNextTrigger(now, forceAdvance: true);
        }
        item.RefreshDynamicProperties();
        Save();
    }

    public void ResetTimer(string id)
    {
        var item = _reminders.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

        var now = DateTime.Now;
        item.SnoozedUntil = null;
        item.IsEnabled = true;
        item.CalculateNextTrigger(now, forceAdvance: true);
        item.RefreshDynamicProperties();
        Save();
    }

    public void TriggerTestNotification(ReminderItem? item = null)
    {
        var target = item ?? NextUpcomingReminder ?? _reminders.FirstOrDefault() ?? new ReminderItem
        {
            Title = "Minum Air Putih (1 Gelas)",
            Category = "Hidrasi",
            Icon = "💧",
            Notes = "Ini adalah tes pengingat dari DRANOTCH. Jaga kesehatan & hidrasi Anda!",
            Type = ReminderType.Interval,
            IntervalMinutes = 60
        };

        FireNotification(target, DateTime.Now);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _timer.Stop();
    }
}
