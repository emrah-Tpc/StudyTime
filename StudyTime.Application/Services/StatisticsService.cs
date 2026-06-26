using System.Globalization;
using StudyTime.Application.DTOs.Statistics;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;
using StudyTime.Domain.Services;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class StatisticsService(
        IStudySessionRepository studySessionRepository,
        ITaskRepository taskRepository,
        ILessonRepository lessonRepository,
        ICurrentUserService currentUserService,
        ProductivityCalculator productivityCalculator) : IStatisticsService
    {
        public async Task<StatisticsSummaryDto> GetStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            // F11: Sunucu saat dilimi yerine kullanıcının offset'i ile yerel gün/saat hesapla.
            var offset = currentUserService.UtcOffsetMinutes;
            DateTime ToUserLocal(DateTime utc) => utc.AddMinutes(offset);

            var allSessions = await studySessionRepository.GetByDateRangeAsync(startDate, endDate);
            var tasks       = await taskRepository.GetByDateRangeAsync(startDate, endDate);
            var allLessons  = await lessonRepository.GetAllAsync();

            // ── Çalışma ve mola oturumlarını ayır ────────────────────────────
            var workSessions  = allSessions.Where(s => !s.IsBreak).ToList();
            var breakSessions = allSessions.Where(s =>  s.IsBreak).ToList();

            var summary = new StatisticsSummaryDto();

            // 1. Temel Toplamlar (yalnızca çalışma oturumları)
            summary.TotalStudyTime = TimeSpan.FromMinutes(workSessions.Sum(s => s.CurrentDuration.TotalMinutes));
            summary.TotalBreakTime = TimeSpan.FromMinutes(breakSessions.Sum(s => s.CurrentDuration.TotalMinutes));
            summary.TotalTasksCompleted = tasks.Count(t => t.Status == TaskStatus.Completed);

            var totalDays = (endDate - startDate).TotalDays;
            if (totalDays > 0)
                summary.AverageDailyStudyMinutes = Math.Round(summary.TotalStudyTime.TotalMinutes / totalDays);

            // 2. Verimlilik Skoru — Domain Service
            summary.ProductivityScore = productivityCalculator.CalculateScore(
                workSessions, tasks, startDate, endDate);

            // 3. Ders İstatistikleri (mola hariç)
            summary.LessonStatistics = workSessions
                .Where(s => s.Lesson != null)
                .GroupBy(s => s.LessonId)
                .Select(g =>
                {
                    var lesson       = allLessons.FirstOrDefault(l => l.Id == g.Key);
                    var lessonTasks  = tasks.Where(t => t.LessonId == g.Key).ToList();
                    var totalTasks   = lessonTasks.Count;
                    var completedTasks = lessonTasks.Count(t => t.Status == TaskStatus.Completed);
                    var rate = totalTasks == 0 ? 0 : (int)((double)completedTasks / totalTasks * 100);

                    return new LessonStatisticDto
                    {
                        LessonName           = lesson?.Name ?? "Unknown",
                        Color                = lesson?.Color ?? "#3b82f6",
                        TotalDurationMinutes = g.Sum(s => s.CurrentDuration.TotalMinutes),
                        TaskCompletionRate   = rate
                    };
                })
                .OrderByDescending(l => l.TotalDurationMinutes)
                .ToList();

            // 4. Görev İstatistikleri (Top 5) — gerçek oturum süresi
            var taskDurations = workSessions
                .Where(s => s.TaskId.HasValue)
                .GroupBy(s => s.TaskId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            // Top 5: çalışma süresi olan görevler (tamamlanma şartı kaldırıldı — "En Uzun Süren Görevler").
            // Eski hâlde yalnız "tamamlanmış VE TaskId ile başlatılmış" görevler geldiğinden grafik genelde boştu.
            summary.TaskStatistics = tasks
                .Where(t => taskDurations.ContainsKey(t.Id))
                .Select(t => new TaskStatisticDto
                {
                    Title           = t.Title,
                    LessonName      = t.Lesson?.Name ?? "-",
                    DurationMinutes = taskDurations.GetValueOrDefault(t.Id, 0),
                    IsCompleted     = t.Status == TaskStatus.Completed
                })
                .OrderByDescending(t => t.DurationMinutes)
                .Take(5)
                .ToList();

            // 5. Günlük Çalışma Trendi (mola hariç) — local-time ile grupla (UTC kayması önlenir)
            var dailyWork = workSessions
                .GroupBy(s => ToUserLocal(s.StartedAt).Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            var trCulture = new CultureInfo("tr-TR");
            var trendData = new List<TimeTrendDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                trendData.Add(new TimeTrendDto
                {
                    Label = date.ToString("dd MMM", trCulture),
                    Value = Math.Floor(dailyWork.GetValueOrDefault(date, 0))
                });
            }
            summary.StudyTrends = trendData;

            // 6. Sliding Window (3-hour blocks) Peak Productivity
            var minutesByHour = new double[24];
            foreach (var s in workSessions)
                minutesByHour[ToUserLocal(s.StartedAt).Hour] += s.CurrentDuration.TotalMinutes;

            var windows = new List<ProductivityDto>();
            for (var h = 0; h <= 21; h++)
            {
                var blockScore = minutesByHour[h] + minutesByHour[h + 1] + minutesByHour[h + 2];
                windows.Add(new ProductivityDto
                {
                    Hour  = h,
                    Score = Math.Round(blockScore, 1),
                    Label = $"{h:D2}:00-{h + 3:D2}:00"
                });
            }

            var maxWindowScore = windows.Max(w => w.Score);
            if (maxWindowScore > 0)
            {
                var peak = windows.First(w => w.Score == maxWindowScore);
                peak.IsPeakRange = true;
            }
            summary.PeakProductivity = windows;

            // 7. Yeni Metrikler (mola hariç)
            summary.TotalSessions = workSessions.Count;
            summary.AverageSessionDuration = workSessions.Any()
                ? Math.Round(workSessions.Average(s => s.CurrentDuration.TotalMinutes), 1)
                : 0;

            if (workSessions.Any())
            {
                var bestDayGroup = workSessions
                    .GroupBy(s => ToUserLocal(s.StartedAt).DayOfWeek)
                    .OrderByDescending(g => g.Sum(s => s.CurrentDuration.TotalMinutes))
                    .FirstOrDefault();

                if (bestDayGroup != null)
                {
                    summary.MostProductiveDay = trCulture.DateTimeFormat.GetDayName(bestDayGroup.Key);
                    summary.MostProductiveDay = char.ToUpper(summary.MostProductiveDay[0])
                                              + summary.MostProductiveDay[1..];
                }
            }

            // 8. Günlük Seans Heatmap (mola hariç)
            var dailySessionGroups = workSessions
                .GroupBy(s => ToUserLocal(s.StartedAt).Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var count = g.Count();
                        var dominantLesson = g
                            .GroupBy(s => s.LessonId)
                            .OrderByDescending(lg => lg.Sum(s => s.CurrentDuration.TotalMinutes))
                            .FirstOrDefault();
                        var dominantColor = allLessons
                            .FirstOrDefault(l => l.Id == dominantLesson?.Key)?.Color ?? "#3b82f6";
                        return (count, dominantColor);
                    });

            var heatmapData = new List<DailySessionDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (dailySessionGroups.TryGetValue(date, out var dayInfo))
                    heatmapData.Add(new DailySessionDto
                        { Date = date, SessionCount = dayInfo.count, DominantColor = dayInfo.dominantColor });
                else
                    heatmapData.Add(new DailySessionDto { Date = date, SessionCount = 0 });
            }
            summary.DailySessionCounts = heatmapData;

            return summary;
        }
    }
}
