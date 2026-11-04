using System;
using System.Collections.Generic;
using NotchMusic.Models;

namespace NotchMusic.Services.Reminder;

public interface IReminderService : IDisposable
{
    event Action<ReminderNotificationEvent>? ReminderTriggered;
    event Action? RemindersUpdated;

    IReadOnlyList<ReminderItem> Reminders { get; }
    IReadOnlyList<ReminderPresetOption> Presets { get; }
    ReminderItem? NextUpcomingReminder { get; }

    void Start();
    void Stop();
    void Load();
    void Save();

    ReminderItem AddReminder(ReminderItem item);
    ReminderItem AddFromPreset(string presetId);
    void UpdateReminder(ReminderItem item);
    void DeleteReminder(string id);
    void ToggleReminder(string id);
    void SnoozeReminder(string id, int minutes = 10);
    void CompleteReminderOccurrence(string id);
    void ResetTimer(string id);
    void TriggerTestNotification(ReminderItem? item = null);
}
