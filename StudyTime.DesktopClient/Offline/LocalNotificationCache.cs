using StudyTime.Domain.Entities;

namespace StudyTime.DesktopClient.Offline
{
    public class LocalNotificationCache(LocalDb db, LocalUserContext userContext)
    {
        public async Task<List<NotificationCacheEntry>> GetNotificationsAsync()
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<NotificationCacheEntry>();

            var dbConn = await db.GetAsync();
            return await dbConn.Table<NotificationCacheEntry>()
                .Where(x => x.UserId == uid)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveNotificationsAsync(IEnumerable<Notification> notifications)
        {
            var uid = userContext.UserId;
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
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var dbConn = await db.GetAsync();
            var entry = await dbConn.Table<NotificationCacheEntry>()
                .Where(x => x.Id == id && x.UserId == uid)
                .FirstOrDefaultAsync();
            if (entry != null)
            {
                entry.IsRead = isRead;
                await dbConn.UpdateAsync(entry);
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var dbConn = await db.GetAsync();
            var unread = await dbConn.Table<NotificationCacheEntry>()
                .Where(x => x.UserId == uid && !x.IsRead)
                .ToListAsync();
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
