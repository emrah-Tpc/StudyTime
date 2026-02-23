using System.Text.Json;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Karmaşık API yanıtlarını (Dashboard, Statistics) JSON snapshot olarak SQLite'ta saklar.
    /// Key: "Dashboard" | "Statistics_week" | "Statistics_month" | "Statistics_year"
    /// </summary>
    public class LocalSnapshotCache(LocalDb db)
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen key'e karşılık gelen snapshot'ı döndürür.
        /// Cache boşsa null döner.
        /// </summary>
        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            var conn  = await db.GetAsync();
            var entry = await conn.Table<SnapshotCacheEntry>()
                                  .Where(e => e.Key == key)
                                  .FirstOrDefaultAsync();

            if (entry == null || string.IsNullOrEmpty(entry.JsonValue))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(entry.JsonValue, _json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cache'e son yazılan zamanı döndürür. Henüz cache yoksa null.
        /// </summary>
        public async Task<DateTime?> GetCachedAtAsync(string key)
        {
            var conn  = await db.GetAsync();
            var entry = await conn.Table<SnapshotCacheEntry>()
                                  .Where(e => e.Key == key)
                                  .FirstOrDefaultAsync();
            return entry?.CachedAt;
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// Verilen DTO'yu JSON olarak snapshot'a yazar (upsert).
        /// </summary>
        public async Task SetAsync<T>(string key, T value)
        {
            var conn  = await db.GetAsync();
            var entry = new SnapshotCacheEntry
            {
                Key      = key,
                JsonValue = JsonSerializer.Serialize(value, _json),
                CachedAt  = DateTime.UtcNow
            };
            await conn.InsertOrReplaceAsync(entry);
        }
    }
}
