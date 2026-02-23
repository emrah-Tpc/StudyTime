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
                await _conn.CreateTableAsync<SnapshotCacheEntry>();
                await _conn.CreateTableAsync<StudySessionCacheEntry>();
                await _conn.CreateTableAsync<NotificationCacheEntry>();

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
    }
}
