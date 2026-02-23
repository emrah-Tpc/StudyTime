using System;
using System.Collections.Generic;
using System.Linq;
using StudyTime.DesktopClient.Offline;
using StudyTime.Domain.Entities;

namespace StudyTime.DesktopClient.Services
{
    public class AppNotificationCenterService
    {
        private readonly StudyTime.DesktopClient.Offline.SyncedNotificationApiService _syncedApi;
        private List<AppNotification> _notifications = new();
        public IReadOnlyList<AppNotification> Notifications => _notifications.AsReadOnly();

        public event Action? OnNotificationsChanged;
        public event Action<AppNotification>? OnNewNotification;
        public event Action? OnCenterStateChanged;

        public bool IsCenterOpen { get; private set; }
        public int UnreadCount => _notifications.Count(n => !n.IsRead);

        public AppNotificationCenterService(StudyTime.DesktopClient.Offline.SyncedNotificationApiService syncedApi)
        {
            _syncedApi = syncedApi;
        }

        public async Task LoadNotificationsAsync()
        {
            try
            {
                var notes = await _syncedApi.GetNotificationsAsync();
                _notifications = notes.Select(n => new AppNotification
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Category = Enum.TryParse<NotificationCategory>(n.Category, out var cat) ? cat : NotificationCategory.System,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead,
                    ActionUrl = n.ActionUrl
                }).ToList();
                OnNotificationsChanged?.Invoke();
            }
            catch { }
        }

        public void AddNotification(string title, string message, NotificationCategory category, string? actionUrl = null)
        {
            // Bu metod artık sadece UI-only bildirimler için (anlık) veya API'ye gönderilmeli
            // Şimdilik API entegrasyonu Loading üzerinden yürüyecek.
        }

        public async void MarkAsRead(Guid id)
        {
            await _syncedApi.MarkAsReadAsync(id);
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                notification.IsRead = true;
                OnNotificationsChanged?.Invoke();
            }
        }

        public async void MarkAllAsReadAsync()
        {
            await _syncedApi.MarkAllAsReadAsync();
            foreach (var n in _notifications) n.IsRead = true;
            OnNotificationsChanged?.Invoke();
        }

        public void ToggleCenter()
        {
            IsCenterOpen = !IsCenterOpen;
            if (IsCenterOpen) LoadNotificationsAsync(); // Açılınca yenile
            OnCenterStateChanged?.Invoke();
        }

        public void CloseCenter()
        {
            if (IsCenterOpen)
            {
                IsCenterOpen = false;
                OnCenterStateChanged?.Invoke();
            }
        }

        public void OpenCenter()
        {
            if (!IsCenterOpen)
            {
                IsCenterOpen = true;
                LoadNotificationsAsync();
                OnCenterStateChanged?.Invoke();
            }
        }

        public void ClearAll()
        {
            _notifications.Clear();
            OnNotificationsChanged?.Invoke();
        }
    }

    public class AppNotification
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public NotificationCategory Category { get; set; }
        public string? ActionUrl { get; set; }

        public string Icon => Category switch
        {
            NotificationCategory.Discipline => "bi-stopwatch",
            NotificationCategory.Motivation => "bi-trophy",
            NotificationCategory.Awareness => "bi-lightbulb",
            NotificationCategory.System => "bi-gear",
            _ => "bi-bell"
        };

        public string Color => Category switch
        {
            NotificationCategory.Discipline => "#f59e0b", // Amber
            NotificationCategory.Motivation => "#ec4899", // Pink
            NotificationCategory.Awareness => "#3b82f6",  // Blue
            NotificationCategory.System => "#10b981",     // Green
            _ => "#fff"
        };
    }

    public enum NotificationCategory
    {
        Discipline,
        Motivation,
        Awareness,
        System
    }
}
