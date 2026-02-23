using StudyTime.Application.DTOs.Statistics;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="StatisticsApiService"/> için Cache-First dekoratörü.
    /// Her "range" değeri için ayrı snapshot key'i kullanılır:
    /// "Statistics_week" | "Statistics_month" | "Statistics_year"
    /// </summary>
    public class SyncedStatisticsApiService(
        StatisticsApiService remote,
        LocalSnapshotCache   cache,
        ConnectivityService  connectivity)
    {
        // ── İstatistik Verileri ───────────────────────────────────────────────

        /// <summary>
        /// İstatistik özetini getirir.
        /// Online → API'den alır, range bazlı key ile snapshot'a yazar.
        /// Offline → SnapshotCache'ten okur.
        /// </summary>
        public async Task<StatisticsSummaryDto?> GetStatisticsAsync(string range)
        {
            var key = $"Statistics_{range}";

            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetStatisticsAsync(range);
                    if (fresh != null)
                        await cache.SetAsync(key, fresh);
                    return fresh;
                }
                catch
                {
                    // API erişilemez → cache'e düş
                }
            }

            return await cache.GetAsync<StatisticsSummaryDto>(key);
        }

        /// <summary>
        /// Belirtilen range için cache'in son güncellenme zamanını döndürür.
        /// </summary>
        public Task<DateTime?> GetLastUpdatedAsync(string range)
            => cache.GetCachedAtAsync($"Statistics_{range}");
    }
}
