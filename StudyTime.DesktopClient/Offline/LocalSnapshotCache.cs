using System.Text.Json;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Karmaşık API yanıtlarını (Dashboard, Statistics) JSON snapshot olarak SQLite'ta saklar.
    /// Anahtarlar kullanıcı Id ile öneklenir; başka hesaba veri sızmasını önler.
    /// </summary>
    public class LocalSnapshotCache(LocalDb db, LocalUserContext userContext)
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private string ScopeKey(string key)
        {
            var u = userContext.UserId;
            return string.IsNullOrEmpty(u) ? $"__nouser__:{key}" : $"{u}:{key}";
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            var conn  = await db.GetAsync();
            var sk    = ScopeKey(key);
            var entry = await conn.Table<SnapshotCacheEntry>()
                .Where(e => e.Key == sk)
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

        public async Task<DateTime?> GetCachedAtAsync(string key)
        {
            var conn  = await db.GetAsync();
            var sk    = ScopeKey(key);
            var entry = await conn.Table<SnapshotCacheEntry>()
                .Where(e => e.Key == sk)
                .FirstOrDefaultAsync();
            return entry?.CachedAt;
        }

        public async Task SetAsync<T>(string key, T value)
        {
            var conn  = await db.GetAsync();
            var sk    = ScopeKey(key);
            var entry = new SnapshotCacheEntry
            {
                Key       = sk,
                JsonValue = JsonSerializer.Serialize(value, _json),
                CachedAt  = DateTime.UtcNow
            };
            await conn.InsertOrReplaceAsync(entry);
        }

        /// <summary>Tek bir mantıksal anahtarın snapshot'ını siler (oturum bitince panel/istatistik tazeleme için).</summary>
        public async Task RemoveAsync(string key)
        {
            var conn = await db.GetAsync();
            var sk   = ScopeKey(key);
            await conn.ExecuteAsync("DELETE FROM [SnapshotCache] WHERE [Key] = ?", sk);
        }

        /// <summary>Dashboard ve tüm istatistik aralığı snapshot'larını siler.</summary>
        public async Task InvalidateDashboardAndStatisticsAsync()
        {
            await RemoveAsync("Dashboard");
            await RemoveAsync("Statistics_7days");
            await RemoveAsync("Statistics_30days");
            await RemoveAsync("Statistics_3months");
        }

        /// <summary>Tüm snapshot satırlarını siler (logout / wipe).</summary>
        public async Task ClearAllAsync()
        {
            var conn = await db.GetAsync();
            await conn.DeleteAllAsync<SnapshotCacheEntry>();
        }
    }
}
