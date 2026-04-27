namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Çalışma oturumu geçmişini SQLite'ta önbellekler (read-only).
    /// Okumalar <see cref="LocalUserContext.UserId"/> ile filtrelenir.
    /// </summary>
    public class LocalStudySessionCache(LocalDb db, LocalUserContext userContext)
    {
        public async Task<List<StudySessionCacheEntry>> GetRecentAsync(int count = 50)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<StudySessionCacheEntry>();

            var conn = await db.GetAsync();
            var list = await conn.Table<StudySessionCacheEntry>()
                .Where(e => e.UserId == uid)
                .ToListAsync();
            return list.OrderByDescending(e => e.StartedAt).Take(count).ToList();
        }

        public async Task<List<StudySessionCacheEntry>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<StudySessionCacheEntry>();

            var conn    = await db.GetAsync();
            var entries = await conn.Table<StudySessionCacheEntry>()
                .Where(e => e.UserId == uid)
                .ToListAsync();
            return entries
                .Where(e => e.StartedAt.Date >= start.Date && e.StartedAt.Date <= end.Date)
                .OrderByDescending(e => e.StartedAt)
                .ToList();
        }

        /// <summary>
        /// API'den gelen oturum listesini toplu upsert eder (UserId atanır).
        /// </summary>
        public async Task UpsertAllAsync(IEnumerable<StudySessionCacheEntry> entries)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            var list = entries.Select(e =>
            {
                e.UserId = uid;
                return e;
            }).ToList();

            if (list.Count == 0) return;

            try
            {
                await conn.RunInTransactionAsync(tran =>
                {
                    foreach (var e in list)
                        tran.InsertOrReplace(e);
                });
            }
            catch
            {
                foreach (var e in list)
                {
                    try { await conn.InsertOrReplaceAsync(e); } catch { /* yoksay */ }
                }
            }
        }

        public async Task ClearAsync()
        {
            var conn = await db.GetAsync();
            await conn.DeleteAllAsync<StudySessionCacheEntry>();
        }
    }
}
