using System.Globalization;
using System.Text.Json;
using StudyTime.Application;
using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Statistics;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Öğlerin boş kalmaması için).
    /// </summary>
    internal static class LocalSessionAnalytics
    {
        private sealed record Interval(DateTime StartUtc, DateTime EndUtc, Guid LessonId, Guid? TaskId, bool IsBreak);

        public static (DateTime StartDate, DateTime EndDate) GetStatisticsRange(string range)
        {
            var today = DateTime.Today;
            return range switch
            {
                "30days" => (today.AddDays(-29), today),
                "3months" => (today.AddMonths(-3), today),
                _ => (today.AddDays(-6), today),
            };
        }

        private static async Task<List<Interval>> CollectIntervalsAsync(LocalDb db, LocalUserContext userContext)
        {
            var list = new List<Interval>();
            var conn = await db.GetAsync();

            var uid = userContext.UserId;
            var cachedSessions = new Dictionary<Guid, StudySessionCacheEntry>();
            if (!string.IsNullOrEmpty(uid))
            {
                var rows = await conn.Table<StudySessionCacheEntry>()
                    .Where(e => e.UserId == uid)
                    .ToListAsync();
                foreach (var e in rows)
                {
                    cachedSessions[e.Id] = e;
                }
            }

            var pending = await conn.Table<OutboxEntry>().OrderBy(e => e.CreatedAt).ToListAsync();
            var starts = new Dictionary<Guid, StudySessionStartPayload>();
            foreach (var entry in pending)
            {
                if (entry.EntityType != "StudySession" || entry.Operation != "Start")
                    continue;
                var p = JsonSerializer.Deserialize<StudySessionStartPayload>(entry.Payload);
                if (p != null)
                    starts[p.LocalSessionId] = p;
            }

            var processedSessionIds = new HashSet<Guid>();

            foreach (var entry in pending)
            {
                if (entry.EntityType != "StudySession" || entry.Operation != "Stop")
                    continue;
                
                var stop = JsonSerializer.Deserialize<StudySessionStopPayload>(entry.Payload);
                if (stop == null)
                    continue;

                DateTime startedAt;
                Guid lessonId;
                Guid? taskId;
                bool isBreak;

                if (starts.TryGetValue(stop.LocalSessionId, out var start))
                {
                    startedAt = start.StartedAt;
                    lessonId = start.LessonId;
                    taskId = start.TaskId;
                    isBreak = start.IsBreak;
                }
                else if (cachedSessions.TryGetValue(stop.LocalSessionId, out var cached))
                {
                    startedAt = cached.StartedAt;
                    lessonId = cached.LessonId;
                    taskId = cached.TaskId;
                    isBreak = cached.IsBreak;
                }
                else
                {
                    continue; // Unknown start
                }

                if (stop.StoppedAt > startedAt)
                {
                    list.Add(new Interval(startedAt, stop.StoppedAt, lessonId, taskId, isBreak));
                }
                processedSessionIds.Add(stop.LocalSessionId);
            }

            // Artık Outbox'ta Stop'u olmayan ama Cache'de bitmiş (EndedAt) olanları ekleyelim.
            foreach (var e in cachedSessions.Values)
            {
                if (processedSessionIds.Contains(e.Id))
                    continue; // Outbox'tan zaten eklendi
                
                if (!e.EndedAt.HasValue)
                    continue; // Hala çalışıyor veya yarım kalmış
                
                if (e.EndedAt.Value > e.StartedAt)
                {
                    list.Add(new Interval(e.StartedAt, e.EndedAt.Value, e.LessonId, e.TaskId, e.IsBreak));
                }
            }

            return list;
        }

        private static DateTime ToLocal(DateTime utc)
        {
            var d = utc;
            if (d.Kind == DateTimeKind.Unspecified)
                d = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d;
        }

        private static double Minutes(Interval i) => (i.EndUtc - i.StartUtc).TotalMinutes;

        private static TimeSpan Duration(Interval i) => i.EndUtc - i.StartUtc;

        private static int ChartMinutes(Interval i) => StudyDurationMetrics.ToChartMinutes(Duration(i));

        private static bool InStatisticsRange(Interval i, DateTime start, DateTime end)
        {
            var d = ToLocal(i.StartUtc).Date;
            return d >= start.Date && d <= end.Date;
        }

        public static async Task<StatisticsSummaryDto> BuildStatisticsSummaryAsync(
            string range,
            LocalDb db,
            LocalLessonCache lessonCache,
            LocalTaskCache taskCache,
            LocalUserContext userContext)
        {
            var (startDate, endDate) = GetStatisticsRange(range);
            var allIntervals = await CollectIntervalsAsync(db, userContext);
            var lessons = await lessonCache.GetAllAsync();
            var lessonById = lessons.ToDictionary(l => l.Id);
            var tasks = await taskCache.GetAllAsync();

            var workSessions = allIntervals
                .Where(i => !i.IsBreak && InStatisticsRange(i, startDate, endDate))
                .ToList();
            var breakSessions = allIntervals
                .Where(i => i.IsBreak && InStatisticsRange(i, startDate, endDate))
                .ToList();

            var summary = new StatisticsSummaryDto
            {
                TotalStudyTime = TimeSpan.FromMinutes(workSessions.Sum(Minutes)),
                TotalBreakTime = TimeSpan.FromMinutes(breakSessions.Sum(Minutes)),
            };

            var totalDays = (endDate - startDate).TotalDays;
            if (totalDays > 0)
                summary.AverageDailyStudyMinutes = Math.Round(summary.TotalStudyTime.TotalMinutes / totalDays);

            summary.TotalTasksCompleted = tasks.Count(t =>
                t.Status == TaskStatus.Completed &&
                t.StartDate.HasValue &&
                t.StartDate.Value.Date >= startDate &&
                t.StartDate.Value.Date <= endDate);

            summary.ProductivityScore = CalculateStatisticsProductivity(workSessions, tasks, startDate, endDate);

            summary.LessonStatistics = workSessions
                .GroupBy(i => i.LessonId)
                .Select(g =>
                {
                    lessonById.TryGetValue(g.Key, out var lesson);
                    var lessonTasks = tasks.Where(x => x.LessonId == g.Key).ToList();
                    var tot = lessonTasks.Count;
                    var done = lessonTasks.Count(x => x.Status == TaskStatus.Completed);
                    var rate = tot == 0 ? 0 : (int)((double)done / tot * 100);
                    return new LessonStatisticDto
                    {
                        LessonName = lesson?.Name ?? "Ders",
                        Color = string.IsNullOrEmpty(lesson?.Color) ? "#3b82f6" : lesson!.Color,
                        TotalDurationMinutes = g.Sum(Minutes),
                        TaskCompletionRate = rate,
                    };
                })
                .OrderByDescending(l => l.TotalDurationMinutes)
                .ToList();

            // Offline'da oturum TaskId'si geçici (temp) olabilir; reconciliation sonrası görev cache'i
            // sunucu Id'sine geçtiğinden eşleşmenin kopmaması için temp→server eşlemesi uygulanır.
            var conn = await db.GetAsync();
            var taskTempMap = new Dictionary<Guid, Guid>();
            foreach (var m in await conn.Table<TempIdMapEntry>().Where(x => x.EntityType == "Task").ToListAsync())
                taskTempMap[m.TempId] = m.ServerId;
            Guid ResolveTaskId(Guid id) => taskTempMap.TryGetValue(id, out var sid) ? sid : id;

            var taskDurations = workSessions
                .Where(i => i.TaskId.HasValue)
                .GroupBy(i => ResolveTaskId(i.TaskId!.Value))
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            // Top 5: çalışma süresi olan görevler (tamamlanma şartı kaldırıldı — server ile tutarlı).
            summary.TaskStatistics = tasks
                .Where(t => taskDurations.ContainsKey(t.Id))
                .Select(t =>
                {
                    var ln = "-";
                    if (t.LessonId.HasValue && lessonById.TryGetValue(t.LessonId.Value, out var les))
                        ln = les.Name;
                    return new TaskStatisticDto
                    {
                        Title = t.Title,
                        LessonName = ln,
                        DurationMinutes = taskDurations.GetValueOrDefault(t.Id, 0),
                        IsCompleted = t.Status == TaskStatus.Completed,
                    };
                })
                .OrderByDescending(t => t.DurationMinutes)
                .Take(5)
                .ToList();

            var dailyWork = workSessions
                .GroupBy(i => ToLocal(i.StartUtc).Date)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            var trendData = new List<TimeTrendDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                trendData.Add(new TimeTrendDto
                {
                    Label = date.ToString("dd MMM", new CultureInfo("tr-TR")),
                    Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                        dailyWork.GetValueOrDefault(date, 0) * 60),
                });
            }

            summary.StudyTrends = trendData;

            var minutesByHourOff = new double[24];
            foreach (var i in workSessions)
                minutesByHourOff[ToLocal(i.StartUtc).Hour] += Minutes(i);

            var windowsOff = new List<StudyTime.Application.DTOs.Statistics.ProductivityDto>();
            for (var wh = 0; wh <= 21; wh++)
            {
                var blockScore = minutesByHourOff[wh] + minutesByHourOff[wh + 1] + minutesByHourOff[wh + 2];
                windowsOff.Add(new StudyTime.Application.DTOs.Statistics.ProductivityDto
                {
                    Hour  = wh,
                    Score = Math.Round(blockScore, 1),
                    Label = $"{wh:D2}:00-{wh + 3:D2}:00"
                });
            }
            var maxWOff = windowsOff.Max(w => w.Score);
            if (maxWOff > 0) windowsOff.First(w => w.Score == maxWOff).IsPeakRange = true;
            summary.PeakProductivity = windowsOff;

            summary.TotalSessions = workSessions.Count;
            summary.AverageSessionDuration = workSessions.Count > 0
                ? Math.Round(workSessions.Average(Minutes), 1)
                : 0;

            if (workSessions.Count > 0)
            {
                var culture = new CultureInfo("tr-TR");
                var best = workSessions
                    .GroupBy(i => ToLocal(i.StartUtc).DayOfWeek)
                    .OrderByDescending(g => g.Sum(Minutes))
                    .First();
                var name = culture.DateTimeFormat.GetDayName(best.Key);
                summary.MostProductiveDay = char.ToUpper(name[0]) + name[1..];
            }
            else
                summary.MostProductiveDay = "-";

            var dailySessionGroups = workSessions
                .GroupBy(i => ToLocal(i.StartUtc).Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var count = g.Count();
                        var dominantLesson = g
                            .GroupBy(x => x.LessonId)
                            .OrderByDescending(lg => lg.Sum(Minutes))
                            .FirstOrDefault();
                        var color = "#3b82f6";
                        if (dominantLesson != null && lessonById.TryGetValue(dominantLesson.Key, out var les) && !string.IsNullOrEmpty(les.Color))
                            color = les.Color;
                        return (count, color);
                    });

            var heatmap = new List<DailySessionDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (dailySessionGroups.TryGetValue(date, out var info))
                    heatmap.Add(new DailySessionDto { Date = date, SessionCount = info.count, DominantColor = info.color });
                else
                    heatmap.Add(new DailySessionDto { Date = date, SessionCount = 0 });
            }

            summary.DailySessionCounts = heatmap;

            return summary;
        }

        /// <summary>
        /// API istatistik özeti ile yerel oturum cache'ini birleştirir (7/30 gün filtreleri için).
        /// </summary>
        public static async Task EnrichStatisticsWithLocalSessionsAsync(
            StatisticsSummaryDto api,
            string range,
            LocalDb db,
            LocalLessonCache lessonCache,
            LocalTaskCache taskCache,
            LocalUserContext userContext)
        {
            StatisticsSummaryDto local;
            try
            {
                local = await BuildStatisticsSummaryAsync(range, db, lessonCache, taskCache, userContext);
            }
            catch
            {
                return;
            }

            if (local.TotalSessions == 0)
                return;

            var apiTrendSum = api.StudyTrends.Sum(t => t.Value);
            var localTrendSum = local.StudyTrends.Sum(t => t.Value);
            var apiStudyMinutes = api.TotalStudyTime.TotalMinutes;
            var localStudyMinutes = local.TotalStudyTime.TotalMinutes;

            var useLocalSessions =
                localStudyMinutes > apiStudyMinutes + 0.5
                || (apiTrendSum == 0 && localTrendSum > 0)
                || local.TotalSessions > api.TotalSessions;

            if (!useLocalSessions)
                return;

            api.TotalStudyTime = local.TotalStudyTime;
            api.TotalBreakTime = local.TotalBreakTime;
            api.AverageDailyStudyMinutes = local.AverageDailyStudyMinutes;
            api.ProductivityScore = local.ProductivityScore;
            api.LessonStatistics = local.LessonStatistics;
            api.TaskStatistics = local.TaskStatistics;
            api.StudyTrends = local.StudyTrends;
            api.PeakProductivity = local.PeakProductivity;
            api.MostProductiveDay = local.MostProductiveDay;
            api.AverageSessionDuration = local.AverageSessionDuration;
            api.TotalSessions = local.TotalSessions;
            api.DailySessionCounts = local.DailySessionCounts;

            if (api.TotalTasksCompleted < local.TotalTasksCompleted)
                api.TotalTasksCompleted = local.TotalTasksCompleted;
        }

        private static int CalculateStatisticsProductivity(
            List<Interval> workSessions,
            List<TaskDto> tasks,
            DateTime startDate,
            DateTime endDate)
        {
            var totalTime = workSessions.Sum(Minutes);
            var rangeTasks = tasks
                .Where(t => t.StartDate.HasValue && t.StartDate.Value.Date >= startDate && t.StartDate.Value.Date <= endDate)
                .ToList();
            if (rangeTasks.Count == 0)
                return 0;

            var completed = rangeTasks.Count(t => t.Status == TaskStatus.Completed);
            var total = rangeTasks.Count(t => t.Status != TaskStatus.Cancelled);
            if (total == 0)
                return 0;

            var taskScore = (double)completed / total * 100;
            var timeScore = Math.Min(totalTime / 240 * 100, 100);
            return (int)((taskScore * 0.6) + (timeScore * 0.4));
        }

        public static async Task ApplyDashboardChartsAsync(
            DashboardSummaryDto dto,
            LocalDb db,
            LocalLessonCache lessonCache,
            LocalTaskCache taskCache,
            LocalUserContext userContext)
        {
            var today = DateTime.Today;
            var allIntervals = await CollectIntervalsAsync(db, userContext);
            var lessons = await lessonCache.GetAllAsync();
            var lessonById = lessons.ToDictionary(l => l.Id);
            var tasks = await taskCache.GetAllAsync();

            var rangeStart = today.AddDays(-13);
            var workSessions = allIntervals
                .Where(i => !i.IsBreak && ToLocal(i.StartUtc).Date >= rangeStart && ToLocal(i.StartUtc).Date <= today)
                .ToList();

            var todayMinutes = workSessions
                .Where(i => ToLocal(i.StartUtc).Date == today)
                .Sum(ChartMinutes);

            if (todayMinutes > 0)
                dto.TodayStudiedMinutes = todayMinutes;

            var lastWeekSameDay = today.AddDays(-7);
            var lastWeekMinutes = workSessions
                .Where(i => ToLocal(i.StartUtc).Date == lastWeekSameDay)
                .Sum(ChartMinutes);
            dto.StudyTimeChange = todayMinutes - lastWeekMinutes;

            var sessionByDate = workSessions
                .GroupBy(i => ToLocal(i.StartUtc).Date)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            dto.WeeklyChartData = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = today.AddDays(-(6 - i));
                    var dayName = date.ToString("ddd", new CultureInfo("tr-TR"));
                    return new ChartDataDto
                    {
                        Label = $"{dayName} {date.Day}",
                        Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                            sessionByDate.GetValueOrDefault(date, 0) * 60),
                    };
                })
                .ToList();

            var sessionByHour = workSessions
                .Where(i => ToLocal(i.StartUtc).Date == today)
                .GroupBy(i => ToLocal(i.StartUtc).Hour)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            int firstHour, lastHour;
            if (sessionByHour.Count > 0)
            {
                firstHour = Math.Max(0, sessionByHour.Keys.Min() - 1);
                lastHour = Math.Min(23, sessionByHour.Keys.Max() + 1);
            }
            else
            {
                firstHour = 8;
                lastHour = 20;
            }

            dto.DailyChartData = Enumerable.Range(firstHour, lastHour - firstHour + 1)
                .Select(h => new ChartDataDto
                {
                    Label = $"{h:D2}:00",
                    Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                        sessionByHour.GetValueOrDefault(h, 0) * 60),
                })
                .ToList();

            dto.CategoryChartData = workSessions
                .GroupBy(i =>
                    {
                        if (lessonById.TryGetValue(i.LessonId, out var l))
                            return l.Type;
                        return LessonType.Academic;
                    })
                .Select(g =>
                {
                    var typeName = g.Key switch
                    {
                        LessonType.Academic => "Okul",
                        LessonType.Personal => "Kişisel",
                        LessonType.Work => "İş",
                        _ => g.Key.ToString(),
                    };
                    var dominantLessonId = g
                        .GroupBy(x => x.LessonId)
                        .OrderByDescending(lg => lg.Sum(Minutes))
                        .First().Key;
                    var color = lessonById.TryGetValue(dominantLessonId, out var dl) && !string.IsNullOrEmpty(dl.Color)
                        ? dl.Color
                        : "#6b7280";
                    return new ChartDataDto
                    {
                        Label = typeName,
                        Value = g.Sum(ChartMinutes),
                        Color = color,
                    };
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            var todayWork = workSessions.Where(i => ToLocal(i.StartUtc).Date == today).ToList();
            var todayTasks = tasks.Where(t => t.StartDate?.Date == today).ToList();
            var tasksForScore = todayTasks.Count > 0 ? todayTasks : tasks;
            dto.ProductivityScore = CalculateDashboardProductivity(todayWork, tasksForScore);

            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = today.AddDays(-diff).Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            dto.CompletedThisWeek = tasks.Count(t =>
                t.Status == TaskStatus.Completed &&
                (t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            dto.CompletedThisMonth = tasks.Count(t =>
                t.Status == TaskStatus.Completed &&
                (t.StartDate ?? DateTime.MinValue).Date >= startOfMonth);

            var minutesByLesson = allIntervals
                .Where(i => !i.IsBreak)
                .GroupBy(i => i.LessonId)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            foreach (var ws in dto.Workspaces)
            {
                var m = minutesByLesson.GetValueOrDefault(ws.LessonId, 0);
                ws.TotalTimeTracked = m < 60
                    ? $"{Math.Ceiling(m)}m"
                    : $"{Math.Round(m / 60.0, 1)}h";
            }
        }

        private static int CalculateDashboardProductivity(List<Interval> todayWork, List<TaskDto> tasksForScore)
        {
            var totalTime = todayWork.Sum(Minutes);
            var completed = tasksForScore.Count(t => t.Status == TaskStatus.Completed);
            var total = tasksForScore.Count(t => t.Status != TaskStatus.Cancelled);
            if (total == 0)
                return 0;
            var taskScore = (double)completed / total * 100;
            var timeScore = Math.Min(totalTime / 240 * 100, 100);
            return (int)((taskScore * 0.6) + (timeScore * 0.4));
        }

        /// <summary>
        /// API özeti ile yerel SQLite oturum cache'ini birleştirir.
        /// Online stop sonrası API gecikse bile grafikler ve bugünkü dakika güncellenir.
        /// </summary>
        public static async Task EnrichDashboardWithLocalSessionsAsync(
            DashboardSummaryDto dto,
            LocalDb db,
            LocalLessonCache lessonCache,
            LocalUserContext userContext)
        {
            var today = DateTime.Today;
            var allIntervals = await CollectIntervalsAsync(db, userContext);
            var lessons = await lessonCache.GetAllAsync();
            var lessonById = lessons.ToDictionary(l => l.Id);

            var localToday = allIntervals
                .Where(i => !i.IsBreak && ToLocal(i.StartUtc).Date == today)
                .ToList();

            if (localToday.Count == 0)
                return;

            var localTodayMinutes = localToday.Sum(ChartMinutes);
            var apiTodayChartMinutes = dto.DailyChartData.Sum(x => x.Value);

            if (localTodayMinutes > dto.TodayStudiedMinutes)
                dto.TodayStudiedMinutes = localTodayMinutes;

            if (localTodayMinutes <= 0)
                return;

            if (apiTodayChartMinutes >= localTodayMinutes && dto.CategoryChartData.Sum(x => x.Value) >= localTodayMinutes)
                return;

            RebuildTodayChartsFromLocal(dto, localToday, lessonById, today);
        }

        private static void RebuildTodayChartsFromLocal(
            DashboardSummaryDto dto,
            List<Interval> localToday,
            Dictionary<Guid, LessonListItemDto> lessonById,
            DateTime today)
        {
            var todayMinutes = localToday.Sum(ChartMinutes);

            var sessionByHour = localToday
                .GroupBy(i => ToLocal(i.StartUtc).Hour)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            int firstHour, lastHour;
            if (sessionByHour.Count > 0)
            {
                firstHour = Math.Max(0, sessionByHour.Keys.Min() - 1);
                lastHour = Math.Min(23, sessionByHour.Keys.Max() + 1);
            }
            else
            {
                firstHour = 8;
                lastHour = 20;
            }

            dto.DailyChartData = Enumerable.Range(firstHour, lastHour - firstHour + 1)
                .Select(h => new ChartDataDto
                {
                    Label = $"{h:D2}:00",
                    Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                        sessionByHour.GetValueOrDefault(h, 0) * 60),
                })
                .ToList();

            if (dto.WeeklyChartData.Count > 0)
            {
                var daySuffix = $" {today.Day}";
                foreach (var bar in dto.WeeklyChartData)
                {
                    if (bar.Label.EndsWith(daySuffix, StringComparison.Ordinal))
                        bar.Value = Math.Max(bar.Value, todayMinutes);
                }
            }

            dto.CategoryChartData = localToday
                .GroupBy(i =>
                {
                    if (lessonById.TryGetValue(i.LessonId, out var l))
                        return l.Type;
                    return LessonType.Academic;
                })
                .Select(g =>
                {
                    var typeName = g.Key switch
                    {
                        LessonType.Academic => "Okul",
                        LessonType.Personal => "Kişisel",
                        LessonType.Work => "İş",
                        _ => g.Key.ToString(),
                    };
                    var dominantLessonId = g
                        .GroupBy(x => x.LessonId)
                        .OrderByDescending(lg => lg.Sum(Minutes))
                        .First().Key;
                    var color = lessonById.TryGetValue(dominantLessonId, out var dl) && !string.IsNullOrEmpty(dl.Color)
                        ? dl.Color
                        : "#6b7280";
                    return new ChartDataDto
                    {
                        Label = typeName,
                        Value = g.Sum(ChartMinutes),
                        Color = color,
                    };
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            var minutesByLesson = localToday
                .GroupBy(i => i.LessonId)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            foreach (var ws in dto.Workspaces)
            {
                if (!minutesByLesson.TryGetValue(ws.LessonId, out var m))
                    continue;
                var total = (int)Math.Ceiling(m);
                ws.TotalTimeTracked = total < 60
                    ? $"{total}m"
                    : $"{Math.Round(m / 60.0, 1)}h";
            }
        }
    }
}
