using System.Collections.Concurrent;
using Microsoft.Maui.Storage;
using StudyTime.DesktopClient.Interfaces;
using StudyTime.DesktopClient.Offline;
using DomainTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.DesktopClient.Services;

/// <summary>
/// Yaklaşan görevleri periyodik kontrol eder ve bildirim merkezine bildirir.
/// </summary>
public sealed class TaskReminderNotificationService : IDisposable
{
    private const int ReminderWindowMinutes = 30;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    private readonly SyncedTaskApiService _taskApi;
    private readonly IAppNotificationService _notifications;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, byte> _sessionSent = new();
    private readonly Task _runner;

    public TaskReminderNotificationService(
        SyncedTaskApiService taskApi,
        IAppNotificationService notifications)
    {
        _taskApi = taskApi;
        _notifications = notifications;
        _runner = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
            await Task.Delay(StartupDelay, _cts.Token);
            while (!_cts.IsCancellationRequested)
            {
                await CheckUpcomingTasksAsync();
                await Task.Delay(PollInterval, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task CheckUpcomingTasksAsync()
    {
        var now = DateTime.Now;
        var horizon = now.AddMinutes(ReminderWindowMinutes);
        var tasks = await _taskApi.GetTasksByDateRangeAsync(now.Date, horizon.AddDays(1).Date);

        foreach (var task in tasks)
        {
            if (!task.EndDate.HasValue || task.Status != DomainTaskStatus.Pending)
                continue;

            var due = task.EndDate.Value;
            if (due < now || due > horizon)
                continue;

            var notificationKey = $"task-reminder:{task.Id}:{due:yyyyMMddHHmm}";
            if (WasAlreadySent(notificationKey))
                continue;

            var remaining = due - now;
            var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            await _notifications.SendNotificationAsync(
                NotificationCategory.Awareness,
                "Yaklaşan Görev",
                $"'{task.Title}' görevinin bitişine {minutes} dk kaldı.");

            MarkSent(notificationKey);
        }
    }

    private bool WasAlreadySent(string key)
    {
        if (_sessionSent.ContainsKey(key))
            return true;

        if (Preferences.Default.Get(key, false))
        {
            _sessionSent.TryAdd(key, 1);
            return true;
        }

        return false;
    }

    private void MarkSent(string key)
    {
        _sessionSent.TryAdd(key, 1);
        Preferences.Default.Set(key, true);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

