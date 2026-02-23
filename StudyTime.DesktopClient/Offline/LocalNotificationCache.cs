using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StudyTime.Domain.Entities;

namespace StudyTime.DesktopClient.Offline
{
    public class LocalNotificationCache
    {
        private readonly LocalDb _db;

        public LocalNotificationCache(LocalDb db)
        {
            _db = db;
        }

        public async Task<List<NotificationCacheEntry>> GetNotificationsAsync()
        {
            var db = await _db.GetAsync();
            return await db.Table<NotificationCacheEntry>()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveNotificationsAsync(IEnumerable<Notification> notifications)
        {
            var db = await _db.GetAsync();
            
            // Mevcutları temizlemek yerine update/insert yapıyoruz
            foreach (var note in notifications)
            {
                var entry = new NotificationCacheEntry
                {
                    Id = note.Id,
                    Title = note.Title,
                    Message = note.Message,
                    Category = note.Category,
                    IsRead = note.IsRead,
                    ActionUrl = note.ActionUrl,
                    CreatedAt = note.CreatedAt,
                    CachedAt = DateTime.Now
                };
                await db.InsertOrReplaceAsync(entry);
            }
        }

        public async Task UpdateReadStatusAsync(Guid id, bool isRead)
        {
            var db = await _db.GetAsync();
            var entry = await db.FindAsync<NotificationCacheEntry>(id);
            if (entry != null)
            {
                entry.IsRead = isRead;
                await db.UpdateAsync(entry);
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            var db = await _db.GetAsync();
            var unread = await db.Table<NotificationCacheEntry>().Where(x => !x.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await db.UpdateAllAsync(unread);
        }
    }
}
