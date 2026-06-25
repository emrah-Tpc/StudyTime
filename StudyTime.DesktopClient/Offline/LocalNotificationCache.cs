using StudyTime.Domain.Entities;
using Microsoft.Maui.Storage;

namespace StudyTime.DesktopClient.Offline
{
    public class LocalNotificationCache(LocalDb db, LocalUserContext userContext)
    {
        private async Task<string?> ResolveUserIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(userContext.UserId))
                return userContext.UserId;

            return await SecureStorage.Default.GetAsync("studytime_local_owner_sub");
        }

        public async Task ReconcileIdAsync(Guid oldId, Guid newId)
        {
            if (oldId == newId)
                return;

            var uid = await ResolveUserIdAsync();
            if (string.IsNullOrEmpty(uid))
                return;

            var dbConn = await db.GetAsync();
            var existing = await dbConn.Table<NotificationCacheEntry>()
                .Where(x => x.Id == oldId && x.UserId == uid)
                .FirstOrDefaultAsync();

            if (existing == null)
                return;

            existing.Id = newId;
            await dbConn.InsertOrReplaceAsync(existing);
            await dbConn.DeleteAsync<NotificationCacheEntry>(oldId);
        }

        public async Task<List<NotificationCacheEntry>> GetNotificationsAsync()
        {
            var dbConn = await db.GetAsync();
            var uid = await ResolveUserIdAsync();

            if (string.IsNullOrEmpty(uid))
            {
                // Son care: owner bulunamiyorsa en azindan local kayitlari goster.
                return await dbConn.Table<NotificationCacheEntry>()
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
            }

            return await dbConn.Table<NotificationCacheEntry>()
                .Where(x => x.UserId == uid)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Tek bir NotificationCacheEntry kaydını SQLite'a yazar veya günceller.
        /// Yeni oluşturulan (offline) bildirimler için kullanılır.
        /// </summary>
        public async Task SaveNotificationEntryAsync(NotificationCacheEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.UserId))
                entry.UserId = await ResolveUserIdAsync();

            var dbConn = await db.GetAsync();
            await dbConn.InsertOrReplaceAsync(entry);
        }

        public async Task SaveNotificationsAsync(IEnumerable<Notification> notifications)
        {
            var uid = await ResolveUserIdAsync();
            if (string.IsNullOrEmpty(uid))
                return;

            var dbConn = await db.GetAsync();

            foreach (var note in notifications)
            {
                var entry = new NotificationCacheEntry
                {
                    Id        = note.Id,
                    Title     = note.Title,
                    Message   = note.Message,
                    Category  = note.Category,
                    IsRead    = note.IsRead,
                    ActionUrl = note.ActionUrl,
                    CreatedAt = note.CreatedAt,
                    CachedAt  = DateTime.Now,
                    UserId    = uid
                };
                await dbConn.InsertOrReplaceAsync(entry);
            }
        }

        public async Task UpdateReadStatusAsync(Guid id, bool isRead)
        {
            var dbConn = await db.GetAsync();
            var uid = await ResolveUserIdAsync();
            NotificationCacheEntry? entry;
            if (string.IsNullOrEmpty(uid))
            {
                entry = await dbConn.Table<NotificationCacheEntry>()
                    .Where(x => x.Id == id)
                    .FirstOrDefaultAsync();
            }
            else
            {
                entry = await dbConn.Table<NotificationCacheEntry>()
                    .Where(x => x.Id == id && x.UserId == uid)
                    .FirstOrDefaultAsync();
            }

            if (entry != null)
            {
                entry.IsRead = isRead;
                await dbConn.UpdateAsync(entry);
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            var dbConn = await db.GetAsync();
            var uid = await ResolveUserIdAsync();
            List<NotificationCacheEntry> unread;

            if (string.IsNullOrEmpty(uid))
            {
                unread = await dbConn.Table<NotificationCacheEntry>()
                    .Where(x => !x.IsRead)
                    .ToListAsync();
            }
            else
            {
                unread = await dbConn.Table<NotificationCacheEntry>()
                    .Where(x => x.UserId == uid && !x.IsRead)
                    .ToListAsync();
            }

            foreach (var n in unread) n.IsRead = true;
            await dbConn.UpdateAllAsync(unread);
        }

        public async Task ClearAsync()
        {
            var dbConn = await db.GetAsync();
            await dbConn.DeleteAllAsync<NotificationCacheEntry>();
        }
    }
}
