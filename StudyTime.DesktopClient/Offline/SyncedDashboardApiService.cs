using StudyTime.Application.DTOs.Dashboard;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="DashboardApiService"/> için Cache-First dekoratörü.
    /// Online → API'den al, snapshot'a yaz, döndür.
    /// Offline → SnapshotCache'ten oku; boşsa null döndür.
    /// </summary>
    public class SyncedDashboardApiService(
        DashboardApiService  remote,
        LocalSnapshotCache   cache,
        ConnectivityService  connectivity)
    {
        private const string CacheKey = "Dashboard";

        // ── Özet Verileri ─────────────────────────────────────────────────────

        /// <summary>
        /// Dashboard özet verisini getirir.
        /// Online ise API'den alır ve snapshot'ı günceller.
        /// Offline ise son alınan snapshot'ı döndürür.
        /// </summary>
        public async Task<DashboardSummaryDto?> GetSummaryAsync()
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetSummaryAsync();
                    if (fresh != null)
                        await cache.SetAsync(CacheKey, fresh);
                    return fresh;
                }
                catch
                {
                    // API erişilemez → cache'e düş
                }
            }

            return await cache.GetAsync<DashboardSummaryDto>(CacheKey);
        }

        /// <summary>
        /// Dashboard verisinin cache'te ne zaman güncellendiğini döndürür.
        /// UI'da "Son güncelleme: X dakika önce" göstermek için kullanılır.
        /// </summary>
        public Task<DateTime?> GetLastUpdatedAsync()
            => cache.GetCachedAtAsync(CacheKey);
    }
}
