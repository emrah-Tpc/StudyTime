using StudyTime.Application.DTOs.Statistics;
using StudyTime.DesktopClient;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="StatisticsApiService"/> için Cache-First dekoratörü.
    /// Her "range" değeri için ayrı snapshot key'i kullanılır:
    /// "Statistics_7days" | "Statistics_30days" | "Statistics_3months"
    /// </summary>
    public class SyncedStatisticsApiService(
        StatisticsApiService remote,
        LocalSnapshotCache   cache,
        ConnectivityService  connectivity,
        LocalDb              db,
        LocalLessonCache     lessonCache,
        LocalTaskCache       taskCache,
        LocalUserContext     userContext,
        StudyTimeAppOptions  appOptions)
    {
        // ── İstatistik Verileri ───────────────────────────────────────────────

        /// <summary>
        /// İstatistik özetini getirir.
        /// Online → API'den alır, range bazlı key ile snapshot'a yazar.
        /// Offline veya snapshot kullanılmıyorsa → yerel outbox + StudySessionCache ile özet; veri yoksa sıfırlar.
        /// </summary>
        public async Task<StatisticsSummaryDto> GetStatisticsAsync(string range)
        {
            var key = $"Statistics_{range}";

            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetStatisticsAsync(range);
                    if (fresh != null)
                    {
                        await LocalSessionAnalytics.EnrichStatisticsWithLocalSessionsAsync(
                            fresh, range, db, lessonCache, taskCache, userContext);
                        await cache.SetAsync(key, fresh);
                        return fresh;
                    }
                }
                catch
                {
                    // API erişilemez → cache / boş
                }
            }

            var skipStaleSnapshot = MauiProgram.IsOfflineBeta || appOptions.LocalOnlyMode;
            if (!skipStaleSnapshot)
            {
                var cached = await cache.GetAsync<StatisticsSummaryDto>(key);
                if (cached != null)
                {
                    await LocalSessionAnalytics.EnrichStatisticsWithLocalSessionsAsync(
                        cached, range, db, lessonCache, taskCache, userContext);
                    return cached;
                }
            }

            try
            {
                return await LocalSessionAnalytics.BuildStatisticsSummaryAsync(range, db, lessonCache, taskCache, userContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncedStatisticsApiService] Yerel istatistik üretilemedi: {ex.Message}");
                return CreateEmptyStatisticsSummary();
            }
        }

        private static StatisticsSummaryDto CreateEmptyStatisticsSummary()
        {
            return new StatisticsSummaryDto
            {
                TotalStudyTime              = TimeSpan.Zero,
                TotalBreakTime              = TimeSpan.Zero,
                AverageDailyStudyMinutes    = 0,
                TotalTasksCompleted         = 0,
                ProductivityScore           = 0,
                LessonStatistics            = new List<LessonStatisticDto>(),
                TaskStatistics              = new List<TaskStatisticDto>(),
                StudyTrends                 = new List<TimeTrendDto>(),
                PeakProductivity            = new List<ProductivityDto>(),
                MostProductiveDay           = "-",
                AverageSessionDuration      = 0,
                TotalSessions               = 0,
                DailySessionCounts          = new List<DailySessionDto>()
            };
        }

        /// <summary>
        /// Belirtilen range için cache'in son güncellenme zamanını döndürür.
        /// </summary>
        public Task<DateTime?> GetLastUpdatedAsync(string range)
            => cache.GetCachedAtAsync($"Statistics_{range}");
    }
}
