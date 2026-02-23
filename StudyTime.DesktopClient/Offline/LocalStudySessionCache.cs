namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Çalışma oturumu geçmişini SQLite'ta önbellekler (read-only).
    /// StudySessionApiService'den gelen liste buraya yansıtılır.
    /// </summary>
    public class LocalStudySessionCache(LocalDb db)
    {
        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>Son <paramref name="count"/> oturumu yeniden eskilere doğru döndürür.</summary>
        public async Task<List<StudySessionCacheEntry>> GetRecentAsync(int count = 50)
        {
            var conn = await db.GetAsync();
            return await conn.Table<StudySessionCacheEntry>()
                             .OrderByDescending(e => e.StartedAt)
                             .Take(count)
                             .ToListAsync();
        }

        /// <summary>Tarih aralığına göre oturumları döndürür.</summary>
        public async Task<List<StudySessionCacheEntry>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            var conn    = await db.GetAsync();
            var entries = await conn.Table<StudySessionCacheEntry>().ToListAsync();
            return entries
                .Where(e => e.StartedAt.Date >= start.Date && e.StartedAt.Date <= end.Date)
                .OrderByDescending(e => e.StartedAt)
                .ToList();
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// API'den gelen oturum listesini toplu upsert eder.
        /// </summary>
        public async Task UpsertAllAsync(IEnumerable<StudySessionCacheEntry> entries)
        {
            var conn = await db.GetAsync();
            var list = entries.ToList();
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
                // Fallback: tek tek dene
                foreach (var e in list)
                {
                    try { await conn.InsertOrReplaceAsync(e); } catch { /* yoksay */ }
                }
            }
        }

        /// <summary>Tüm cache'i temizler.</summary>
        public async Task ClearAsync()
        {
            var conn = await db.GetAsync();
            await conn.DeleteAllAsync<StudySessionCacheEntry>();
        }
    }
}
