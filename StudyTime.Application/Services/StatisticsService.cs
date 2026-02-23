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
        ProductivityCalculator productivityCalculator) : IStatisticsService
    {
        public async Task<StatisticsSummaryDto> GetStatisticsAsync(DateTime startDate, DateTime endDate)
        {
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

            // 2. Verimlilik Skoru — Domain Service (DashboardService ile aynı formül)
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

            // 4. Görev İstatistikleri (Top 5)
            summary.TaskStatistics = tasks
                .Where(t => t.Status == TaskStatus.Completed)
                .OrderByDescending(t => t.PlannedDuration.GetValueOrDefault().TotalMinutes)
                .Take(5)
                .Select(t => new TaskStatisticDto
                {
                    Title           = t.Title,
                    LessonName      = t.Lesson?.Name ?? "-",
                    DurationMinutes = t.PlannedDuration.GetValueOrDefault().TotalMinutes,
                    IsCompleted     = true
                })
                .ToList();

            // 5. Günlük Çalışma Trendi (mola hariç, sıfır doldurma)
            var dailyWork = workSessions
                .GroupBy(s => s.StartedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            var trendData = new List<TimeTrendDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                trendData.Add(new TimeTrendDto
                {
                    Label = date.ToString("dd MMM", new System.Globalization.CultureInfo("tr-TR")),
                    Value = dailyWork.GetValueOrDefault(date, 0)
                });
            }
            summary.StudyTrends = trendData;

            // 6. Günün Saatlerine Göre Verimlilik (mola hariç)
            summary.PeakProductivity = workSessions
                .GroupBy(s => s.StartedAt.Hour)
                .Select(g => new ProductivityDto
                {
                    Hour  = g.Key,
                    Score = g.Sum(s => s.CurrentDuration.TotalMinutes)
                })
                .OrderBy(p => p.Hour)
                .ToList();

            // 7. Yeni Metrikler (mola hariç)
            summary.TotalSessions = workSessions.Count;
            summary.AverageSessionDuration = workSessions.Any()
                ? Math.Round(workSessions.Average(s => s.CurrentDuration.TotalMinutes), 1)
                : 0;

            if (workSessions.Any())
            {
                var culture      = new System.Globalization.CultureInfo("tr-TR");
                var bestDayGroup = workSessions
                    .GroupBy(s => s.StartedAt.DayOfWeek)
                    .OrderByDescending(g => g.Sum(s => s.CurrentDuration.TotalMinutes))
                    .FirstOrDefault();

                if (bestDayGroup != null)
                {
                    summary.MostProductiveDay = culture.DateTimeFormat.GetDayName(bestDayGroup.Key);
                    summary.MostProductiveDay = char.ToUpper(summary.MostProductiveDay[0])
                                              + summary.MostProductiveDay[1..];
                }
            }

            // 8. Günlük Seans Heatmap (mola hariç)
            var dailySessionGroups = workSessions
                .GroupBy(s => s.StartedAt.Date)
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
