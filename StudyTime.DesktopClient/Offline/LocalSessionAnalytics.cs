using System.Globalization;
using System.Text.Json;
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

            foreach (var entry in pending)
            {
                if (entry.EntityType != "StudySession" || entry.Operation != "Stop")
                    continue;
                var stop = JsonSerializer.Deserialize<StudySessionStopPayload>(entry.Payload);
                if (stop == null)
                    continue;
                if (!starts.TryGetValue(stop.LocalSessionId, out var start))
                    continue;
                if (stop.StoppedAt <= start.StartedAt)
                    continue;
                list.Add(new Interval(start.StartedAt, stop.StoppedAt, start.LessonId, start.TaskId, start.IsBreak));
            }

            var uid = userContext.UserId;
            if (!string.IsNullOrEmpty(uid))
            {
                var rows = await conn.Table<StudySessionCacheEntry>()
                    .Where(e => e.UserId == uid)
                    .ToListAsync();
                foreach (var e in rows)
                {
                    if (!e.EndedAt.HasValue)
                        continue;
                    var end = e.EndedAt.Value;
                    if (end <= e.StartedAt)
                        continue;
                    list.Add(new Interval(e.StartedAt, end, e.LessonId, e.TaskId, e.IsBreak));
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

            var taskDurations = workSessions
                .Where(i => i.TaskId.HasValue)
                .GroupBy(i => i.TaskId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(Minutes));

            summary.TaskStatistics = tasks
                .Where(t => t.Status == TaskStatus.Completed && taskDurations.ContainsKey(t.Id))
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
                        IsCompleted = true,
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
                    Value = dailyWork.GetValueOrDefault(date, 0),
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

            var todayMinutes = (int)Math.Round(workSessions
                .Where(i => ToLocal(i.StartUtc).Date == today)
                .Sum(Minutes));

            if (todayMinutes > 0)
                dto.TodayStudiedMinutes = todayMinutes;

            var lastWeekSameDay = today.AddDays(-7);
            var lastWeekMinutes = (int)Math.Round(workSessions
                .Where(i => ToLocal(i.StartUtc).Date == lastWeekSameDay)
                .Sum(Minutes));
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
                        Value = (int)Math.Round(sessionByDate.GetValueOrDefault(date, 0)),
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
                    Value = (int)Math.Round(sessionByHour.GetValueOrDefault(h, 0)),
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
                        Value = (int)Math.Round(g.Sum(Minutes)),
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
    }
}
