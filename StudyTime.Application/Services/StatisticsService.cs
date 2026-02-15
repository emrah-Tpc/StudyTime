using StudyTime.Application.DTOs.Statistics;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class StatisticsService(
        IStudySessionRepository studySessionRepository,
        ITaskRepository taskRepository,
        ILessonRepository lessonRepository) : IStatisticsService
    {
        public async Task<StatisticsSummaryDto> GetStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var sessions = await studySessionRepository.GetByDateRangeAsync(startDate, endDate);
            var tasks = await taskRepository.GetByDateRangeAsync(startDate, endDate);
            var allLessons = await lessonRepository.GetAllAsync();

            var summary = new StatisticsSummaryDto();

            // 1. Basic Totals
            summary.TotalStudyTime = TimeSpan.FromMinutes(sessions.Sum(s => s.CurrentDuration.TotalMinutes));
            summary.TotalTasksCompleted = tasks.Count(t => t.Status == TaskStatus.Completed);

            var totalDays = (endDate - startDate).TotalDays;
            if (totalDays > 0)
            {
                summary.AverageDailyStudyMinutes = Math.Round(summary.TotalStudyTime.TotalMinutes / totalDays);
            }

            // 2. Productivity Score (Simplified Logic)
            // Example: 1 point for every 5 mins studied + 5 points for every task completed
            // Cap at 100 per day average logic or similar. For now, simple calc.
            var score = (summary.TotalStudyTime.TotalMinutes / 5) + (summary.TotalTasksCompleted * 5);
            summary.ProductivityScore = Math.Min((int)score, 100); // Placeholder logic

            // 3. Lesson Statistics
            summary.LessonStatistics = sessions
                .Where(s => s.Lesson != null)
                .GroupBy(s => s.LessonId)
                .Select(g =>
                {
                    var lesson = allLessons.FirstOrDefault(l => l.Id == g.Key);
                    var lessonTasks = tasks.Where(t => t.LessonId == g.Key).ToList();
                    var totalTasks = lessonTasks.Count;
                    var completedTasks = lessonTasks.Count(t => t.Status == TaskStatus.Completed);
                    var rate = totalTasks == 0 ? 0 : (int)((double)completedTasks / totalTasks * 100);

                    return new LessonStatisticDto
                    {
                        LessonName = lesson?.Name ?? "Unknown",
                        Color = lesson?.Color ?? "#3b82f6",
                        TotalDurationMinutes = g.Sum(s => s.CurrentDuration.TotalMinutes),
                        TaskCompletionRate = rate
                    };
                })
                .OrderByDescending(l => l.TotalDurationMinutes)
                .ToList();

            // 4. Task Statistics (Top 5)
            summary.TaskStatistics = tasks
                .Where(t => t.Status == TaskStatus.Completed) 
                .OrderByDescending(t => t.PlannedDuration.GetValueOrDefault().TotalMinutes) 
                .Take(5) // Limit to 5
                .Select(t => new TaskStatisticDto
                {
                    Title = t.Title,
                    LessonName = t.Lesson?.Name ?? "-",
                    DurationMinutes = t.PlannedDuration.GetValueOrDefault().TotalMinutes, 
                    IsCompleted = t.Status == TaskStatus.Completed
                })
                .ToList();

            // 5. Study Trends (Daily) with Zero Filling
            var dailyGroups = sessions
                .GroupBy(s => s.StartedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            var trendData = new List<TimeTrendDto>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                trendData.Add(new TimeTrendDto
                {
                    Label = date.ToString("dd MMM", new System.Globalization.CultureInfo("tr-TR")),
                    Value = dailyGroups.ContainsKey(date) ? dailyGroups[date] : 0
                });
            }
            summary.StudyTrends = trendData;

            // 6. Peak Productivity (Hour of Day)
            summary.PeakProductivity = sessions
                .GroupBy(s => s.StartedAt.Hour)
                .Select(g => new ProductivityDto
                {
                    Hour = g.Key,
                    Score = g.Sum(s => s.CurrentDuration.TotalMinutes)
                })
                .OrderBy(p => p.Hour)
                .ToList();

            // 7. New Metrics Calculation
            summary.TotalSessions = sessions.Count;
            summary.AverageSessionDuration = sessions.Any() 
                ? Math.Round(sessions.Average(s => s.CurrentDuration.TotalMinutes), 1) 
                : 0;

            if (sessions.Any())
            {
                var culture = new System.Globalization.CultureInfo("tr-TR");
                var bestDayGroup = sessions
                    .GroupBy(s => s.StartedAt.DayOfWeek)
                    .OrderByDescending(g => g.Sum(s => s.CurrentDuration.TotalMinutes))
                    .FirstOrDefault();

                if (bestDayGroup != null)
                {
                    summary.MostProductiveDay = culture.DateTimeFormat.GetDayName(bestDayGroup.Key);
                    // Capitalize first letter
                    summary.MostProductiveDay = char.ToUpper(summary.MostProductiveDay[0]) + summary.MostProductiveDay.Substring(1);
                }
            }
            
            return summary;
        }
    }
}
