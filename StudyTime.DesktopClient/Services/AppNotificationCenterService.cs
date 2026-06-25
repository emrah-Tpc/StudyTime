using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StudyTime.DesktopClient.Interfaces;
using StudyTime.DesktopClient.Offline;
using StudyTime.Domain.Entities;

namespace StudyTime.DesktopClient.Services
{
    /// <summary>
    /// Merkezi bildirim kontrol servisi. Context-Aware UX ve Offline-First prensiplerini uygular.
    /// 
    /// - Uygulama Foreground (ön planda) ise: OS bildirimi EZİLİR, sadece UI Toast/zil tetiklenir.
    /// - Uygulama Background/kapalı ise: IPlatformNotificationHandler üzerinden OS bildirimi fırlatılır.
    /// - Single Source of Truth: Tüm bildirimler SQLite + API üzerinden yaşar.
    /// </summary>
    public class AppNotificationCenterService : IAppNotificationService
    {
        private readonly SyncedNotificationApiService  _syncedApi;
        private readonly IPlatformNotificationHandler  _platformHandler;

        private List<AppNotification> _notifications = new();
        public  IReadOnlyList<AppNotification> Notifications => _notifications.AsReadOnly();

        public event Action? OnNotificationsChanged;
        public event Action<AppNotification>? OnNewNotification;
        public event Action? OnCenterStateChanged;

        public bool IsCenterOpen  { get; private set; }
        public int  UnreadCount   => _notifications.Count(n => !n.IsRead);

        public AppNotificationCenterService(
            SyncedNotificationApiService syncedApi,
            IPlatformNotificationHandler platformHandler)
        {
            _syncedApi       = syncedApi;
            _platformHandler = platformHandler;

            // Uygulama acilir acilmaz local/remote bildirimleri cek.
            _ = LoadNotificationsAsync();
        }

        // ── IAppNotificationService ───────────────────────────────────────────

        /// <summary>
        /// Ana bildirim gönderme metodudur. Context-Aware UX uygular.
        /// 1. Offline-First: SQLite'a kaydeder.
        /// 2. Foreground ise: Sadece UI bildirim kutusunu günceller.
        ///    Background ise: OS (Windows Tray/Mobile Push) bildirimini fırlatır.
        /// </summary>
        public async Task SendNotificationAsync(NotificationCategory category, string title, string message)
        {
            // 1. Single Source of Truth: Önce lokale kaydet (Offline-First)
            var notificationId = await _syncedApi.CreateAndSaveOfflineAsync(
                category.ToString(), title, message);

            // 2. UI listesine de ekle (anlık yansıma için)
            var appNotification = new AppNotification
            {
                Id        = notificationId,
                Title     = title,
                Message   = message,
                Category  = category,
                CreatedAt = DateTime.Now,
                IsRead    = false
            };
            _notifications.Insert(0, appNotification);
            OnNotificationsChanged?.Invoke();

            // 3. Context-Aware UX: Uygulama ekranda mı?
            if (IsAppInForeground())
            {
                // Kullanıcı uygulamanın içinde: Sadece UI event'ini tetikle (zil kırmızısın)
                // OS bildirimi (tray balonu/push) ATILMAZ — notification fatigue önlenir.
                OnNewNotification?.Invoke(appNotification);
            }
            else
            {
                // Uygulama arka planda veya kapalı: OS bildirimini fırlat
                _platformHandler.ShowOSNotification(title, message, notificationId);
            }
        }

        /// <summary>
        /// MAUI lifecycle üzerinden uygulamanın ön planda olup olmadığını döner.
        /// </summary>
        public bool IsAppInForeground()
        {
#if WINDOWS
            // Windows'ta uygulama penceresi aktif mi?
            return Microsoft.Maui.ApplicationModel.WindowStateManager.Default.GetActiveWindow() != null;
#else
            // iOS/Android: ApplicationState üzerinden kontrol
            return Microsoft.Maui.ApplicationModel.AppInfo.Current != null
                   && Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0;
#endif
        }

        // ── Bildirim Merkezi UI Yönetimi ──────────────────────────────────────

        public async Task LoadNotificationsAsync()
        {
            try
            {
                var notes = await _syncedApi.GetNotificationsAsync();
                _notifications = notes.Select(n => new AppNotification
                {
                    Id        = n.Id,
                    Title     = n.Title,
                    Message   = n.Message,
                    Category  = Enum.TryParse<NotificationCategory>(n.Category, out var cat)
                                    ? cat : NotificationCategory.System,
                    CreatedAt = n.CreatedAt,
                    IsRead    = n.IsRead,
                    ActionUrl = n.ActionUrl
                }).ToList();
                OnNotificationsChanged?.Invoke();
            }
            catch { /* Sessiz hata — önbellek zaten UI'da */ }
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            await _syncedApi.MarkAsReadAsync(id);
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                notification.IsRead = true;
                OnNotificationsChanged?.Invoke();
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            await _syncedApi.MarkAllAsReadAsync();
            foreach (var n in _notifications) n.IsRead = true;
            OnNotificationsChanged?.Invoke();
        }

        public void ToggleCenter()
        {
            IsCenterOpen = !IsCenterOpen;
            if (IsCenterOpen) _ = LoadNotificationsAsync();
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
                _ = LoadNotificationsAsync();
                OnCenterStateChanged?.Invoke();
            }
        }

        public async Task ClearAllAsync()
        {
            await _syncedApi.ClearAllAsync();
            _notifications.Clear();
            OnNotificationsChanged?.Invoke();
        }
    }

    // ── AppNotification Model ─────────────────────────────────────────────────

    public class AppNotification
    {
        public Guid               Id        { get; set; }
        public string             Title     { get; set; } = string.Empty;
        public string             Message   { get; set; } = string.Empty;
        public DateTime           CreatedAt { get; set; }
        public bool               IsRead    { get; set; }
        public NotificationCategory Category { get; set; }
        public string?            ActionUrl { get; set; }

        public string Icon => Category switch
        {
            NotificationCategory.Discipline => "bi-stopwatch",
            NotificationCategory.Motivation => "bi-trophy",
            NotificationCategory.Awareness  => "bi-lightbulb",
            NotificationCategory.System     => "bi-gear",
            _                               => "bi-bell"
        };

        public string Color => Category switch
        {
            NotificationCategory.Discipline => "#f59e0b",
            NotificationCategory.Motivation => "#ec4899",
            NotificationCategory.Awareness  => "#3b82f6",
            NotificationCategory.System     => "#10b981",
            _                               => "#fff"
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
