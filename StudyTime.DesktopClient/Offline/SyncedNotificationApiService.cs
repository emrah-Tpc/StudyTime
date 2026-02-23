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

        public SyncedNotificationApiService(
            NotificationApiService api, 
            LocalNotificationCache cache, 
            ConnectivityService connectivity)
        {
            _api = api;
            _cache = cache;
            _connectivity = connectivity;
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
                Id = e.Id,
                Title = e.Title,
                Message = e.Message,
                Category = e.Category,
                IsRead = e.IsRead,
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
                    // Outbox'a atılabilir ama bildirim okundu bilgisi kritik değilse es geçilebilir
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
    }
}
