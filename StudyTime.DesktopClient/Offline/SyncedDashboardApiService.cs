using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.DesktopClient.Services;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;
using StudyTime.DesktopClient;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="DashboardApiService"/> için Cache-First dekoratörü.
    /// Online → API'den al, snapshot'a yaz, döndür.
    /// Offline → SnapshotCache'ten oku; yoksa yerel ders/görev önbelleğinden özet üret.
    /// </summary>
    public class SyncedDashboardApiService(
        DashboardApiService  remote,
        LocalSnapshotCache   cache,
        ConnectivityService  connectivity,
        LocalDb              db,
        LocalLessonCache     lessonCache,
        LocalTaskCache       taskCache,
        LocalUserContext     userContext)
    {
        private const string CacheKey = "Dashboard";

        /// <summary>
        /// Yalnızca yerel snapshot'tan okur (API çağrısı yok). Dashboard ilk boyamada kullanılır.
        /// </summary>
        public Task<DashboardSummaryDto?> TryGetCachedSummaryAsync()
            => cache.GetAsync<DashboardSummaryDto>(CacheKey);

        // ── Özet Verileri ─────────────────────────────────────────────────────

        /// <summary>
        /// Dashboard özet verisini getirir.
        /// Online ise API'den alır ve snapshot'ı günceller.
        /// Offline ise son alınan snapshot'ı döndürür.
        /// </summary>
        public async Task<DashboardSummaryDto?> GetSummaryAsync()
        {
            DashboardSummaryDto? summary = null;
            var builtFromLocalTaskAndLessonCaches = false;

            // Her zaman önce API'den taze veri çekmeye çalış.
            // Stop → navigate → dashboard senaryosunda eski snapshot gösterilmemesi için
            // TTL/cache-first stratejisi kullanmıyoruz; API başarısız olursa cache'e düşüyoruz.
            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetSummaryAsync();
                    if (fresh != null)
                    {
                        await cache.SetAsync(CacheKey, fresh);
                        summary = fresh;
                    }
                }
                catch { /* API ulaşılamaz → snapshot/local fallback */ }
            }

            // API başarısız olduysa snapshot cache'e bak
            if (summary == null)
            {
                summary = await cache.GetAsync<DashboardSummaryDto>(CacheKey);
            }

            // Snapshot da yoksa yerel cache'ten özet üret
            if (summary == null)
            {
                summary = await BuildSummaryFromLocalCachesAsync();
                builtFromLocalTaskAndLessonCaches = summary != null;
            }

            if (summary != null)
            {
                var skipTaskOutbox     = builtFromLocalTaskAndLessonCaches || MauiProgram.IsOfflineBeta;
                var skipSessionOutbox  = builtFromLocalTaskAndLessonCaches;
                summary = await ApplyOfflineDeltasAsync(summary, skipTaskOutbox, skipSessionOutbox);
                await LocalSessionAnalytics.EnrichDashboardWithLocalSessionsAsync(
                    summary, db, lessonCache, userContext);
            }

            return summary;
        }

        /// <summary>
        /// API ve snapshot yokken (Offline Beta / ilk kurulum) yerel SQLite görev ve derslerinden özet üretir.
        /// </summary>
        private async Task<DashboardSummaryDto?> BuildSummaryFromLocalCachesAsync()
        {
            try
            {
                var lessons = await lessonCache.GetAllAsync();
                var tasks   = await taskCache.GetAllAsync();

                var total     = tasks.Count;
                var completed = tasks.Count(t => t.Status == TaskStatus.Completed);
                var cancelled = tasks.Count(t => t.Status == TaskStatus.Cancelled);
                var pending   = tasks.Count(t => t.Status == TaskStatus.Pending);

                var weekStart = StartOfWeekUtcMonday();
                var createdThisWeek = tasks.Count(t =>
                    t.StartDate.HasValue && t.StartDate.Value >= weekStart);

                var dto = new DashboardSummaryDto
                {
                    TotalTasks           = total,
                    TasksCreatedThisWeek = createdThisWeek,
                    PendingTasks         = pending,
                    HighPriorityPending  = 0,
                    CompletedTasks       = completed,
                    CompletionRate       = total > 0 ? (int)Math.Round(100.0 * completed / total) : 0,
                    TodayStudiedMinutes  = 0,
                    StudyTimeChange      = 0,
                    CancelledTasks       = cancelled,
                    ActiveLessons        = lessons.Count,
                    ProductivityScore    = total > 0 ? Math.Clamp((int)Math.Round(100.0 * completed / total), 0, 100) : 0,
                    CompletedThisWeek    = 0,
                    CompletedThisMonth   = 0,
                    DailyChartData       = new List<ChartDataDto>(),
                    WeeklyChartData      = new List<ChartDataDto>(),
                    CategoryChartData    = new List<ChartDataDto>(),
                    Workspaces           = BuildWorkspaces(lessons, tasks),
                    RecentActivities     = BuildRecentActivities(lessons, tasks)
                };

                await LocalSessionAnalytics.ApplyDashboardChartsAsync(dto, db, lessonCache, taskCache, userContext);

                return dto;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTime StartOfWeekUtcMonday()
        {
            var d = DateTime.UtcNow.Date;
            var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return d.AddDays(-diff);
        }

        private static List<DashboardWorkspaceDto> BuildWorkspaces(List<LessonListItemDto> lessons, List<TaskDto> tasks)
        {
            var list = new List<DashboardWorkspaceDto>();
            foreach (var lesson in lessons)
            {
                var lt = tasks.Where(t => t.LessonId == lesson.Id).ToList();
                var tot = lt.Count;
                var done = lt.Count(t => t.Status == TaskStatus.Completed);
                var pend = lt.Count(t => t.Status == TaskStatus.Pending);
                var pct = tot > 0 ? (int)Math.Round(100.0 * done / tot) : 0;
                list.Add(new DashboardWorkspaceDto
                {
                    LessonId       = lesson.Id,
                    Name           = lesson.Name,
                    Color          = string.IsNullOrEmpty(lesson.Color) ? "#38bdf8" : lesson.Color,
                    TotalTasks     = tot,
                    CompletedTasks = done,
                    PendingTasks   = pend,
                    ProgressPercent = pct,
                    TotalTimeTracked = "0m"
                });
            }

            return list;
        }

        private static List<RecentActivityDto> BuildRecentActivities(List<LessonListItemDto> lessons, List<TaskDto> tasks)
        {
            var lessonName = new Dictionary<Guid, string>();
            foreach (var l in lessons)
                lessonName[l.Id] = l.Name;

            return tasks
                .OrderByDescending(t => t.EndDate ?? t.StartDate ?? DateTime.MinValue)
                .Take(8)
                .Select(t =>
                {
                    var ln = t.LessonId.HasValue && lessonName.TryGetValue(t.LessonId.Value, out var n) ? n : "Görev";
                    var done = t.Status == TaskStatus.Completed;
                    return new RecentActivityDto
                    {
                        Id            = t.Id,
                        IsCompleted   = done,
                        Title         = t.Title,
                        Subtitle      = ln,
                        StatusText    = done ? "Tamamlandı" : "Bekliyor",
                        StatusColorClass = done ? "text-success" : "text-primary",
                        IconClass     = ""
                    };
                })
                .ToList();
        }

        private async Task<DashboardSummaryDto> ApplyOfflineDeltasAsync(
            DashboardSummaryDto baseSummary,
            bool skipTaskOutboxAdjustments,
            bool skipSessionOutboxAdjustments)
        {
            try
            {
                var conn = await db.GetAsync();
                var pending = await conn.Table<OutboxEntry>().OrderBy(e => e.CreatedAt).ToListAsync();
                if (!pending.Any()) return baseSummary;

                var sessionStarts = new Dictionary<Guid, StudySessionStartPayload>();
                var sessionStops = new Dictionary<Guid, StudySessionStopPayload>();
                int newTasks = 0;
                int deletedTasks = 0;

                foreach (var entry in pending)
                {
                    if (entry.EntityType == "StudySession")
                    {
                        if (entry.Operation == "Start")
                        {
                            var payload = System.Text.Json.JsonSerializer.Deserialize<StudySessionStartPayload>(entry.Payload);
                            if (payload != null && !payload.IsBreak)
                                sessionStarts[payload.LocalSessionId] = payload;
                        }
                        else if (entry.Operation == "Stop")
                        {
                            var payload = System.Text.Json.JsonSerializer.Deserialize<StudySessionStopPayload>(entry.Payload);
                            if (payload != null)
                                sessionStops[payload.LocalSessionId] = payload;
                        }
                    }
                    else if (entry.EntityType == "Task")
                    {
                        if (entry.Operation == "Create") newTasks++;
                        else if (entry.Operation == "Delete") deletedTasks++;
                    }
                }

                if (!skipSessionOutboxAdjustments)
                {
                    var todayLocal = DateTime.Today;
                    int offlineMinutes = 0;
                    foreach (var start in sessionStarts.Values)
                    {
                        if (!sessionStops.TryGetValue(start.LocalSessionId, out var stop))
                            continue;
                        if (stop.StoppedAt <= start.StartedAt)
                            continue;
                        var startLocal = start.StartedAt.Kind == DateTimeKind.Utc
                            ? start.StartedAt.ToLocalTime()
                            : start.StartedAt;
                        if (startLocal.Date != todayLocal)
                            continue;
                        offlineMinutes += (int)(stop.StoppedAt - start.StartedAt).TotalMinutes;
                    }

                    if (offlineMinutes > 0)
                    {
                        baseSummary.TodayStudiedMinutes += offlineMinutes;
                    }
                }

                if (!skipTaskOutboxAdjustments)
                {
                    baseSummary.TotalTasks += (newTasks - deletedTasks);
                    baseSummary.PendingTasks += (newTasks - deletedTasks);
                    if (baseSummary.PendingTasks < 0) baseSummary.PendingTasks = 0;
                }

                return baseSummary;
            }
            catch (Exception)
            {
                return baseSummary;
            }
        }

        /// <summary>
        /// Dashboard verisinin cache'te ne zaman güncellendiğini döndürür.
        /// UI'da "Son güncelleme: X dakika önce" göstermek için kullanılır.
        /// </summary>
        public Task<DateTime?> GetLastUpdatedAsync()
            => cache.GetCachedAtAsync(CacheKey);
    }
}
