#pragma warning disable CS0067
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Statistics;
using Microsoft.AspNetCore.Components;

namespace StudyTime.DesktopClient.Services
{
    public interface IPlatformDetector
    {
        bool IsDesktop { get; }
        bool IsMobile { get; }
        bool IsWeb { get; }
    }

    public class MockPlatformDetector : IPlatformDetector
    {
        public bool IsDesktop => true;
        public bool IsMobile => false;
        public bool IsWeb => true;
    }

    public class SyncedDashboardApiService
    {
        public Task<DashboardSummaryDto?> TryGetCachedSummaryAsync()
            => Task.FromResult<DashboardSummaryDto?>(null);

        public Task<DashboardSummaryDto?> GetSummaryAsync()
            => Task.FromResult<DashboardSummaryDto?>(new DashboardSummaryDto());
    }

    public class SyncedTaskApiService
    {
        public event Action? OnTasksChanged;
        public Task<List<TaskListItemDto>> GetTasksAsync(DateTime? date = null, bool includeCompleted = false) => Task.FromResult(new List<TaskListItemDto>());
        public Task<List<StudyTime.Application.DTOs.Tasks.TaskDto>> GetTasksByDateRangeAsync(DateTime start, DateTime end, bool includeCompleted = false) => Task.FromResult(new List<StudyTime.Application.DTOs.Tasks.TaskDto>());
        public Task UpdateTaskStatusAsync(Guid id, StudyTime.Domain.Enums.TaskStatus status) => Task.CompletedTask;
        public Task UpdateTaskStatusAsync(int id, bool isCompleted) => Task.CompletedTask;
        public Task<StudyTime.Application.DTOs.Tasks.TaskDto> GetTaskByIdAsync(int id) => Task.FromResult(new StudyTime.Application.DTOs.Tasks.TaskDto());
        public Task<StudyTime.Application.DTOs.Tasks.TaskDto> GetTaskByIdAsync(Guid id) => Task.FromResult(new StudyTime.Application.DTOs.Tasks.TaskDto());
        public Task<bool> CreateAsync(object dto) => Task.FromResult(true);
        public Task<bool> DeleteAsync(Guid id) => Task.FromResult(true);
    }

    public class SyncedLessonApiService
    {
        public Task<List<LessonListItemDto>> GetLessonsAsync() => Task.FromResult(new List<LessonListItemDto>());
        public Task<List<LessonListItemDto>> GetAllAsync() => Task.FromResult(new List<LessonListItemDto>());
        public Task<WorkspaceDetailDto> GetWorkspaceDetailAsync(Guid id) => Task.FromResult(new WorkspaceDetailDto());
        public Task<bool> RestoreAsync(Guid id) => Task.FromResult(true);
        public Task<bool> ArchiveAsync(Guid id) => Task.FromResult(true);
        public Task<bool> UpdateNotesAsync(Guid id, string notes) => Task.FromResult(true);
        public Task<bool> CreateAsync(object dto) => Task.FromResult(true);
        public Task<bool> UpdateAsync(Guid id, object dto) => Task.FromResult(true);
        public Task<bool> DeleteAsync(Guid id) => Task.FromResult(true);
    }

    public class SyncedStatisticsApiService
    {
        public Task<StatisticsSummaryDto> GetSummaryAsync(string period) => Task.FromResult(new StatisticsSummaryDto());
        public Task<StatisticsSummaryDto> GetDashboardStatisticsAsync() => Task.FromResult(new StatisticsSummaryDto());
        public Task<StatisticsSummaryDto> GetStatisticsAsync(string period) => Task.FromResult(new StatisticsSummaryDto());
    }

    public class StudySessionApiService
    {
        public Task StartSessionAsync(int taskId) => Task.CompletedTask;
        public Task StartSessionAsync(Guid taskId) => Task.CompletedTask;
        public Task StopSessionAsync(int taskId, int durationMinutes) => Task.CompletedTask;
        public Task StopSessionAsync(Guid taskId, int durationMinutes) => Task.CompletedTask;
    }

    public class ThemeService
    {
        public string CurrentTheme { get; set; } = "dark";
        public bool IsDarkMode => CurrentTheme == "dark";
        public event Action? OnThemeChanged;
        public Task ToggleThemeAsync() => Task.CompletedTask;
        public Task InitializeAsync() => Task.CompletedTask;
        public void SetTheme(string theme) { }
        public void SetTheme(bool isDark) { }
    }

    public class AuthService
    {
        public event Action? OnAuthStateChanged;
        public Task<bool> CheckAuthAsync() => Task.FromResult(true);
        public Task LogoutAsync() => Task.CompletedTask;
        public Task<bool> LoginAsync(string email, string pass) => Task.FromResult(true);
        public Task<bool> RegisterAsync(string name, string email, string pass) => Task.FromResult(true);
        public Task<bool> ChangePasswordAsync(object model) => Task.FromResult(true);
        public Task<bool> UpdateProfileAsync(object model) => Task.FromResult(true);
    }

    public class ConnectivityService
    {
        public bool IsConnected => true;
        public bool IsOnline => true;
        public event Action? OnConnectivityChanged;
        public event Action<bool>? OnChanged;
    }

    public class GlobalTimerService
    {
        private int _displayHighWaterMark;
        private int _displayStoppedBridge;
        private int _summaryBaselineMinutes;
        private int _sessionsMinutesAddedLocally;
        private int _displayStateDayOfYear = -1;

        public bool IsRunning { get; set; } = false;
        public bool IsPaused { get; set; } = false;
        public bool IsBreak { get; set; } = false;
        public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;
        public TimeSpan RemainingTime { get; set; } = TimeSpan.Zero;
        public StudyTime.Application.DTOs.Tasks.TaskListItemDto? CurrentTask { get; set; }
        public Guid? ActiveLessonId { get; set; }
        public Guid? ActiveTaskId { get; set; }
        public string ActiveColor { get; set; } = "#ffffff";
        public string ActiveTaskTitle { get; set; } = string.Empty;
        public string ActiveModeName { get; set; } = string.Empty;
        public bool IsFocusModeActive { get; set; } = false;
        public TimeSpan BreakDuration { get; set; } = TimeSpan.Zero;
        public bool IsCountdown { get; set; } = false;
        public TimeSpan InitialDuration { get; set; } = TimeSpan.Zero;

        public int DisplayTodayStudiedMinutes
        {
            get
            {
                EnsureDisplayStateDay();
                var current = ComputeCurrentDisplayMinutes();
                if (current > _displayHighWaterMark)
                    _displayHighWaterMark = current;
                return _displayHighWaterMark;
            }
        }

        public void SetSummaryBaseline(int todayStudiedMinutes)
        {
            EnsureDisplayStateDay();
            _summaryBaselineMinutes = todayStudiedMinutes;
            if (IsRunning && !IsBreak)
            {
                var live = todayStudiedMinutes + ActiveSessionExtraMinutes;
                if (live > _displayHighWaterMark)
                    _displayHighWaterMark = live;
            }
            else
            {
                var current = Math.Max(todayStudiedMinutes, _displayStoppedBridge);
                if (current > _displayHighWaterMark)
                    _displayHighWaterMark = current;
            }
        }

        public void MergeApiSummaryMinutes(int apiTodayMinutes)
        {
            EnsureDisplayStateDay();
            _summaryBaselineMinutes = apiTodayMinutes;

            if (IsRunning && !IsBreak)
            {
                var live = apiTodayMinutes + ActiveSessionExtraMinutes;
                if (live > _displayHighWaterMark)
                    _displayHighWaterMark = live;
                return;
            }

            if (apiTodayMinutes >= _displayHighWaterMark)
            {
                _displayHighWaterMark = apiTodayMinutes;
                _displayStoppedBridge = 0;
                _sessionsMinutesAddedLocally = 0;
            }
        }

        public void ClearDashboardDisplayState()
        {
            _displayHighWaterMark = 0;
            _displayStoppedBridge = 0;
            _summaryBaselineMinutes = 0;
            _sessionsMinutesAddedLocally = 0;
            _displayStateDayOfYear = -1;
        }

        private int ActiveSessionExtraMinutes =>
            IsRunning && !IsBreak ? (int)Math.Floor(ElapsedTime.TotalMinutes) : 0;

        private int ComputeCurrentDisplayMinutes()
        {
            var baseTotal = _summaryBaselineMinutes + _sessionsMinutesAddedLocally;
            if (IsRunning && !IsBreak)
                return baseTotal + ActiveSessionExtraMinutes;
            return Math.Max(baseTotal, _displayStoppedBridge);
        }

        private void EnsureDisplayStateDay()
        {
            var today = DateTime.Today.DayOfYear;
            if (_displayStateDayOfYear == today) return;
            _displayHighWaterMark = 0;
            _displayStoppedBridge = 0;
            _summaryBaselineMinutes = 0;
            _sessionsMinutesAddedLocally = 0;
            _displayStateDayOfYear = today;
        }

        public event Action? OnTimerFinished;
        public event Action? OnBreakFinished;
        public event Action? OnTimerStopped;
        public event Action? OnTick;
        public event Action? OnStateChanged;
        public event Action? OnFocusModeChanged;

        public Task StartAsync(Guid lessonId, Guid? activeTaskId, string color, string? taskTitle = null, TimeSpan? countdown = null, TimeSpan? breakDuration = null) => Task.CompletedTask;
        public Task StartBreakAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void TogglePause() { }
        public void Pause() { }
        public void Resume() { }
        public void SkipBreak() { }
        public void SetFocusMode(bool isFocus) { }
    }
}

namespace StudyTime.DesktopClient.Offline
{
    public class SyncStatusService
    {
        public event Action? OnChange;
        public bool IsSyncing => false;
        public int PendingUploads => 0;
        public bool HasPendingItems => false;
        public string SyncError { get; set; } = string.Empty;
        public Task SyncNowAsync() => Task.CompletedTask;
    }
}

namespace StudyTime.DesktopClient
{
    public class StudyTimeAppOptions
    {
        public string ApiBaseUrl { get; set; } = "http://localhost:5000";
        public bool LocalOnlyMode { get; set; } = false;
    }

    public class AppNotificationCenterService
    {
        public event Action? OnNotificationCountChanged;
        public event Action? OnCenterStateChanged;
        public event Action? OnNotificationsChanged;
        public int UnreadCount => 0;
        public bool IsCenterOpen { get; set; }
        public List<AppNotification> Notifications { get; set; } = new List<AppNotification>();
        public void CloseCenter() { }
        public void ToggleCenter() { }
        public Task RequestPermissionAsync() => Task.CompletedTask;
        public Task<List<AppNotification>> GetNotificationsAsync() => Task.FromResult(new List<AppNotification>());
        public Task MarkAsReadAsync(int id) => Task.CompletedTask;
        public Task MarkAllAsReadAsync() => Task.CompletedTask;
        public Task ClearAllAsync() => Task.CompletedTask;
    }

    public class AppNotification
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
