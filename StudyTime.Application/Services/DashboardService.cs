using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;
using System.Globalization;
using AppTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class DashboardService(
        ITaskRepository taskRepository,
        ILessonRepository lessonRepository,
        IStudySessionRepository studySessionRepository)
    {
        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            // 1. VERİLERİ ÇEK
            var tasks = await taskRepository.GetAllAsync();
            var lessons = await lessonRepository.GetAllAsync();
            var allSessions = await studySessionRepository.GetAllAsync();

            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            // Tarih Hesaplamaları
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = today.AddDays(-1 * diff).Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // 2. TEMEL İSTATİSTİKLER & TRENDLER

            // A. Çalışma Süresi (Bugün vs Dün)
            var todayMinutes = allSessions
                .Where(s => s.StartedAt.Date == today)
                .Sum(s => (int)s.CurrentDuration.TotalMinutes);

            var yesterdayMinutes = allSessions
                .Where(s => s.StartedAt.Date == yesterday)
                .Sum(s => (int)s.CurrentDuration.TotalMinutes);

            int timeChange = todayMinutes - yesterdayMinutes; // Artış veya azalış

            // B. Görev İstatistikleri
            var totalCount = tasks.Count;
            var completedCount = tasks.Count(t => t.Status == AppTaskStatus.Completed);
            var pendingCount = tasks.Count(t => t.Status == AppTaskStatus.Pending);

            // Başarı Oranı
            int completionRate = totalCount == 0 ? 0 : (int)((double)completedCount / totalCount * 100);

            // Bu hafta eklenen görevler (Trend)
            // Not: CreatedAt nullable değilse direkt kullan, nullablesa StartDate kullan
            int tasksCreatedThisWeek = tasks.Count(t => (t.StartDate ?? DateTime.MinValue) >= startOfWeek);

            // C. Verimlilik Skoru (Basit bir algoritma: Başarı oranı %60 + Çalışma süresi %40)
            int productivityScore = 0;
            if (totalCount > 0)
            {
                int taskScore = completionRate; // 0-100 arası
                // Hedef günlük 4 saat (240dk) çalışma olsun
                int timeScore = Math.Min(todayMinutes * 100 / 240, 100);

                productivityScore = (int)(taskScore * 0.6 + timeScore * 0.4);
            }

            // Alt Bilgiler
            var completedThisWeek = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue) >= startOfWeek);
            var completedThisMonth = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue) >= startOfMonth);

            var activeLessons = lessons
                .Where(l => l.Status == LessonStatus.Active && !l.IsDeleted)
                .ToList();

            // 3. HAFTALIK GRAFİK
            var weeklyChartData = new List<ChartDataDto>();
            for (int i = 6; i >= 0; i--)
            {
                var targetDate = today.AddDays(-i);
                var dayName = targetDate.ToString("ddd", new CultureInfo("tr-TR"));
                var dailyMinutes = allSessions
                    .Where(s => s.StartedAt.Date == targetDate)
                    .Sum(s => (int)s.CurrentDuration.TotalMinutes);
                weeklyChartData.Add(new ChartDataDto { Label = dayName, Value = dailyMinutes });
            }

            // 4. KATEGORİ GRAFİĞİ
            var categoryChartData = allSessions
                .GroupBy(s => s.LessonId)
                .Select(g => new ChartDataDto
                {
                    Label = lessons.FirstOrDefault(l => l.Id == g.Key)?.Name ?? "Diğer",
                    Value = (int)g.Sum(s => s.CurrentDuration.TotalMinutes)
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList();

            // 5. SON AKTİVİTELER
            var recentActivities = tasks
                .OrderByDescending(t => t.StartDate)
                .Take(4)
                .Select(t => {
                    bool isCompleted = t.Status == AppTaskStatus.Completed;
                    bool isPending = t.Status == AppTaskStatus.Pending;
                    string statusText = isCompleted ? "Tamamlandı" : (isPending ? "Sürüyor" : "Bekliyor");
                    string colorClass = isCompleted ? "text-success" : (isPending ? "text-primary" : "text-warning");
                    string icon = isCompleted ? "bi-check-lg" : (isPending ? "bi-play-fill" : "bi-clock");
                    var lessonName = lessons.FirstOrDefault(l => l.Id == t.LessonId)?.Name ?? "Genel";
                    var dateRef = t.StartDate ?? DateTime.Now;
                    var timeSpan = DateTime.Now - dateRef;
                    string timeAgo = timeSpan.TotalMinutes < 60 ? $"{Math.Ceiling(timeSpan.TotalMinutes)}dk önce"
                                   : timeSpan.TotalHours < 24 ? $"{Math.Ceiling(timeSpan.TotalHours)}s önce"
                                   : $"{Math.Ceiling(timeSpan.TotalDays)} gün önce";
                    return new RecentActivityDto { Title = t.Title, Subtitle = $"{lessonName} • {timeAgo}", StatusText = statusText, StatusColorClass = colorClass, IconClass = icon };
                }).ToList();

            // 6. WORKSPACES
            var workspaceList = activeLessons.Select(lesson =>
            {
                var lessonTasks = tasks.Where(t => t.LessonId == lesson.Id).ToList();
                int lTotal = lessonTasks.Count;
                int lCompleted = lessonTasks.Count(t => t.Status == AppTaskStatus.Completed);
                int progress = lTotal == 0 ? 0 : (int)((double)lCompleted / lTotal * 100);
                var lessonSessions = allSessions.Where(s => s.LessonId == lesson.Id);
                double totalMinutes = lessonSessions.Sum(s => s.CurrentDuration.TotalMinutes);
                string timeStr = totalMinutes < 60 ? $"{Math.Ceiling(totalMinutes)}m" : $"{Math.Round(totalMinutes / 60, 1)}h";
                return new DashboardWorkspaceDto { LessonId = lesson.Id, Name = lesson.Name, Color = lesson.Color, TotalTasks = lTotal, CompletedTasks = lCompleted, PendingTasks = lTotal - lCompleted, ProgressPercent = progress, TotalTimeTracked = timeStr };
            }).ToList();

            return new DashboardSummaryDto
            {
                TotalTasks = totalCount,
                TasksCreatedThisWeek = tasksCreatedThisWeek, // YENİ
                PendingTasks = pendingCount,
                CompletedTasks = completedCount,
                CompletionRate = completionRate, // YENİ
                TodayStudiedMinutes = todayMinutes,
                StudyTimeChange = timeChange, // YENİ
                ProductivityScore = productivityScore,
                CompletedThisWeek = completedThisWeek,
                CompletedThisMonth = completedThisMonth,
                RecentActivities = recentActivities,
                Workspaces = workspaceList,
                WeeklyChartData = weeklyChartData,
                CategoryChartData = categoryChartData
            };
        }
    }
}