using SQLite;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Platformdan bağımsız SQLite veritabanı factory.
    /// Uygulama boyunca tek bir bağlantı kullanılır (Singleton).
    /// </summary>
    public sealed class LocalDb : IAsyncDisposable
    {
        private SQLiteAsyncConnection? _conn;
        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Bağlantıyı başlatır ve tüm tabloları oluşturur (CREATE TABLE IF NOT EXISTS).
        /// İlk çağrıda çalışır; sonraki çağrılar mevcut bağlantıyı döndürür.
        /// </summary>
        public async Task<SQLiteAsyncConnection> GetAsync()
        {
            if (_conn != null) return _conn;

            await _lock.WaitAsync();
            try
            {
                if (_conn != null) return _conn;

                // Platforma göre uygulamaya özel veri klasörü
                string dbPath = Path.Combine(
                    FileSystem.AppDataDirectory,
                    "studytime_local.db");

                _conn = new SQLiteAsyncConnection(dbPath,
                    SQLiteOpenFlags.ReadWrite |
                    SQLiteOpenFlags.Create    |
                    SQLiteOpenFlags.SharedCache);

                // Tablolar (IF NOT EXISTS — idempotent)
                await _conn.CreateTableAsync<LessonCacheEntry>();
                await _conn.CreateTableAsync<TaskCacheEntry>();
                await _conn.CreateTableAsync<OutboxEntry>();
                await _conn.CreateTableAsync<DeadLetterEntry>();
                await _conn.CreateTableAsync<SessionServerIdMapEntry>();
                await _conn.CreateTableAsync<TempIdMapEntry>();
                await _conn.CreateTableAsync<SnapshotCacheEntry>();
                await _conn.CreateTableAsync<StudySessionCacheEntry>();
                await _conn.CreateTableAsync<NotificationCacheEntry>();

                await MigrateUserIsolationColumnsAsync(_conn);

                return _conn;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_conn != null)
            {
                await _conn.CloseAsync();
                _conn = null;
            }
        }

        /// <summary>
        /// Mevcut kurulumlara UserId / IsDeleted sütunlarını ekler ve kullanıcı bağlamı olmayan eski satırları siler.
        /// </summary>
        private static async Task MigrateUserIsolationColumnsAsync(SQLiteAsyncConnection conn)
        {
            await TryAddColumnAsync(conn, "LessonCache", "UserId", "TEXT");
            await TryAddColumnAsync(conn, "LessonCache", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await TryAddColumnAsync(conn, "TaskCache", "UserId", "TEXT");
            await TryAddColumnAsync(conn, "TaskCache", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await TryAddColumnAsync(conn, "StudySessionCache", "UserId", "TEXT");
            await TryAddColumnAsync(conn, "NotificationCache", "UserId", "TEXT");

            await conn.ExecuteAsync("DELETE FROM LessonCache WHERE UserId IS NULL OR TRIM(IFNULL(UserId,'')) = ''");
            await conn.ExecuteAsync("DELETE FROM TaskCache WHERE UserId IS NULL OR TRIM(IFNULL(UserId,'')) = ''");
            await conn.ExecuteAsync("DELETE FROM StudySessionCache WHERE UserId IS NULL OR TRIM(IFNULL(UserId,'')) = ''");
            await conn.ExecuteAsync("DELETE FROM NotificationCache WHERE UserId IS NULL OR TRIM(IFNULL(UserId,'')) = ''");

            // Eski kullanıcı öneki olmayan snapshot anahtarları (Dashboard vb.) — hesap izolasyonu
            await conn.ExecuteAsync("DELETE FROM SnapshotCache WHERE Instr([Key], ':') = 0");
        }

        private static async Task TryAddColumnAsync(SQLiteAsyncConnection conn, string table, string column, string definition)
        {
            try
            {
                await conn.ExecuteAsync($"ALTER TABLE [{table}] ADD COLUMN {column} {definition}");
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
            }
        }
    }
}
