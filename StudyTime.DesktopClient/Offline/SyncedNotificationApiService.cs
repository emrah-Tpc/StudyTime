using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StudyTime.Domain.Entities;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    public class SyncedNotificationApiService
    {
        private readonly NotificationApiService _api;
        private readonly LocalNotificationCache _cache;
        private readonly ConnectivityService _connectivity;
        private readonly LocalUserContext _userContext;
        private readonly OutboxProcessor _outbox;

        public SyncedNotificationApiService(
            NotificationApiService api,
            LocalNotificationCache cache,
            ConnectivityService connectivity,
            LocalUserContext userContext,
            OutboxProcessor outbox)
        {
            _api          = api;
            _cache        = cache;
            _connectivity = connectivity;
            _userContext  = userContext;
            _outbox       = outbox;
        }

        /// <summary>
        /// Yeni bir bildirimi önce yerel SQLite'a (Offline-First) kaydeder,
        /// ardından çevrimiçiyse API'ye gönderir. Çevrimdışıysa Outbox'a atar.
        /// </summary>
        public async Task<Guid> CreateAndSaveOfflineAsync(
            string category, string title, string message, string? actionUrl = null)
        {
            var localId = Guid.NewGuid();
            var now     = DateTime.UtcNow;

            // 1. Single Source of Truth: Her zaman önce lokale yaz
            var entry = new NotificationCacheEntry
            {
                Id        = localId,
                Title     = title,
                Message   = message,
                Category  = category,
                IsRead    = false,
                ActionUrl = actionUrl,
                CreatedAt = now,
                CachedAt  = now,
                UserId    = _userContext.UserId
            };
            await _cache.SaveNotificationEntryAsync(entry);

            var notification = new Notification
            {
                Id        = localId,
                Title     = title,
                Message   = message,
                Category  = category,
                IsRead    = false,
                ActionUrl = actionUrl,
                CreatedAt = now
            };

            // 2. Çevrimiçiyse API'ye de gönder
            if (_connectivity.IsOnline)
            {
                try
                {
                    var serverId = await _api.CreateAsync(notification);
                    if (serverId != Guid.Empty)
                    {
                        await _cache.ReconcileIdAsync(localId, serverId);
                        return serverId;
                    }

                    return localId;
                }
                catch
                {
                    // API düşerse Outbox'a ekle
                }
            }

            // 3. Çevrimdışıysa Outbox kuyruğuna ekle
            await _outbox.EnqueueAsync("Notification", "Create", notification);
            return localId;
        }

        public async Task<List<Notification>> GetNotificationsAsync()
        {
            if (_connectivity.IsOnline)
            {
                try
                {
                    var remoteNotes = await _api.GetNotificationsAsync();
                    await _cache.SaveNotificationsAsync(remoteNotes);
                    return remoteNotes;
                }
                catch
                {
                    // Fallback to cache on error
                }
            }

            var cachedEntries = await _cache.GetNotificationsAsync();
            return cachedEntries.Select(e => new Notification
            {
                Id        = e.Id,
                Title     = e.Title,
                Message   = e.Message,
                Category  = e.Category,
                IsRead    = e.IsRead,
                ActionUrl = e.ActionUrl,
                CreatedAt = e.CreatedAt
            }).ToList();
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            // Önce lokalde güncelle (UI hızı için)
            await _cache.UpdateReadStatusAsync(id, true);

            if (_connectivity.IsOnline)
            {
                try
                {
                    await _api.MarkAsReadAsync(id);
                }
                catch
                {
                    // Bildirim okundu bilgisi kritik değil, sessiz geç
                }
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            await _cache.MarkAllAsReadAsync();

            if (_connectivity.IsOnline)
            {
                try
                {
                    await _api.MarkAllAsReadAsync();
                }
                catch { }
            }
        }

        public async Task ClearAllAsync()
        {
            // 1. Önce yerel önbelleği temizle
            await _cache.ClearAsync();

            // 2. Çevrimiçiyse API'ye de silme isteği gönder
            if (_connectivity.IsOnline)
            {
                try
                {
                    await _api.DeleteAllAsync();
                }
                catch
                {
                    // Silme işlemi başarısız olsa bile kullanıcı yerelde temizlediği için sorun yok.
                }
            }
        }
    }
}
