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
            var tasks = await taskRepository.GetAllAsync() ?? new();
            var lessons = await lessonRepository.GetAllAsync() ?? new();
            var allSessions = await studySessionRepository.GetAllAsync() ?? new();

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

            int timeChange = todayMinutes - yesterdayMinutes;

            // B. Görev İstatistikleri
            var totalCount = tasks.Count;
            var completedCount = tasks.Count(t => t.Status == AppTaskStatus.Completed);
            var pendingCount = tasks.Count(t => t.Status == AppTaskStatus.Pending);

            // Başarı Oranı
            int completionRate = totalCount == 0 ? 0 : (int)Math.Round((double)completedCount / totalCount * 100);

            // Bu hafta eklenen görevler (Trend)
            int tasksCreatedThisWeek = tasks.Count(t => (t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);

            // C. Verimlilik Skoru (Basit algoritma: Başarı oranı %60 + Çalışma süresi %40)
            int productivityScore = 0;
            if (totalCount > 0 || todayMinutes > 0)
            {
                int taskScore = completionRate;
                // Hedef günlük 4 saat (240dk) çalışma olsun
                int timeScore = Math.Min(todayMinutes * 100 / 240, 100);

                productivityScore = (int)(taskScore * 0.6 + timeScore * 0.4);
            }

            // Alt Bilgiler
            var completedThisWeek = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            var completedThisMonth = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue).Date >= startOfMonth);

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

            // 4. GÜNLÜK GRAFİK (Gece, Sabah, Öğle, Akşam)
            var dailyChartData = new List<ChartDataDto>();
            var todaysSessions = allSessions.Where(s => s.StartedAt.Date == today).ToList();

            dailyChartData.Add(new ChartDataDto { Label = "Gece", Value = todaysSessions.Where(s => s.StartedAt.Hour >= 0 && s.StartedAt.Hour < 6).Sum(s => (int)s.CurrentDuration.TotalMinutes) });
            dailyChartData.Add(new ChartDataDto { Label = "Sabah", Value = todaysSessions.Where(s => s.StartedAt.Hour >= 6 && s.StartedAt.Hour < 12).Sum(s => (int)s.CurrentDuration.TotalMinutes) });
            dailyChartData.Add(new ChartDataDto { Label = "Öğle", Value = todaysSessions.Where(s => s.StartedAt.Hour >= 12 && s.StartedAt.Hour < 18).Sum(s => (int)s.CurrentDuration.TotalMinutes) });
            dailyChartData.Add(new ChartDataDto { Label = "Akşam", Value = todaysSessions.Where(s => s.StartedAt.Hour >= 18 && s.StartedAt.Hour < 24).Sum(s => (int)s.CurrentDuration.TotalMinutes) });

            // 5. KATEGORİ GRAFİĞİ (Gerçek renklerle, .Take kısıtlaması olmadan)
            // 5. KATEGORİ GRAFİĞİ (Dersin kendi rengini dinamik olarak atıyoruz)
            // 5. KATEGORİ GRAFİĞİ (Hata Giderilmiş Versiyon)
            var categoryChartData = allSessions
    .Where(s => s.LessonId != Guid.Empty)
    .GroupBy(s => s.LessonId)
    .Select(g =>
    {
        // 'lessons' listesini yukarıda çektiğimizden emin olmalıyız
        var lesson = lessons.FirstOrDefault(l => l.Id == g.Key);

        return new ChartDataDto
        {
            Label = lesson?.Name ?? "Diğer",
            Value = (int)g.Sum(s => s.CurrentDuration.TotalMinutes),
            // Buradaki Color artık ders oluştururken seçtiğin renktir
            Color = lesson?.Color ?? "#6b7280"
        };
    })
    .Where(x => x.Value > 0)
    .OrderByDescending(x => x.Value)
    .ToList();

            // 6. SON AKTİVİTELER
            var recentActivities = tasks
                .Where(t => t.StartDate.HasValue)
                .OrderByDescending(t => t.StartDate)
                .Take(4)
                .Select(t => {
                    bool isCompleted = t.Status == AppTaskStatus.Completed;
                    bool isPending = t.Status == AppTaskStatus.Pending;

                    string statusText = isCompleted ? "Tamamlandı" : (isPending ? "Sürüyor" : "Bekliyor");
                    string colorClass = isCompleted ? "text-success" : (isPending ? "text-primary" : "text-warning");
                    string icon = isCompleted ? "bi-check-lg" : (isPending ? "bi-play-fill" : "bi-clock");

                    var lessonName = lessons.FirstOrDefault(l => l.Id == t.LessonId)?.Name ?? "Genel";
                    var dateRef = t.StartDate!.Value;
                    var timeSpan = DateTime.Now - dateRef;

                    string timeAgo = timeSpan.TotalMinutes < 60 ? $"{Math.Max(1, Math.Ceiling(timeSpan.TotalMinutes))}dk önce"
                                   : timeSpan.TotalHours < 24 ? $"{Math.Ceiling(timeSpan.TotalHours)}s önce"
                                   : $"{Math.Ceiling(timeSpan.TotalDays)} gün önce";

                    return new RecentActivityDto
                    {
                        Title = t.Title,
                        Subtitle = $"{lessonName} • {timeAgo}",
                        StatusText = statusText,
                        StatusColorClass = colorClass,
                        IconClass = icon
                    };
                }).ToList();

            // 7. WORKSPACES
            var workspaceList = activeLessons.Select(lesson =>
            {
                var lessonTasks = tasks.Where(t => t.LessonId == lesson.Id).ToList();
                int lTotal = lessonTasks.Count;
                int lCompleted = lessonTasks.Count(t => t.Status == AppTaskStatus.Completed);
                int progress = lTotal == 0 ? 0 : (int)Math.Round((double)lCompleted / lTotal * 100);

                var lessonSessions = allSessions.Where(s => s.LessonId == lesson.Id);
                double totalMinutes = lessonSessions.Sum(s => s.CurrentDuration.TotalMinutes);

                string timeStr = totalMinutes < 60 ? $"{Math.Ceiling(totalMinutes)}m" : $"{Math.Round(totalMinutes / 60.0, 1)}h";

                return new DashboardWorkspaceDto
                {
                    LessonId = lesson.Id,
                    Name = lesson.Name,
                    Color = lesson.Color ?? "#3b82f6",
                    TotalTasks = lTotal,
                    CompletedTasks = lCompleted,
                    PendingTasks = lTotal - lCompleted,
                    ProgressPercent = progress,
                    TotalTimeTracked = timeStr
                };
            }).ToList();

            return new DashboardSummaryDto
            {
                TotalTasks = totalCount,
                TasksCreatedThisWeek = tasksCreatedThisWeek,
                PendingTasks = pendingCount,
                CompletedTasks = completedCount,
                CompletionRate = completionRate,
                TodayStudiedMinutes = todayMinutes,
                StudyTimeChange = timeChange,
                ProductivityScore = productivityScore,
                CompletedThisWeek = completedThisWeek,
                CompletedThisMonth = completedThisMonth,
                RecentActivities = recentActivities,
                Workspaces = workspaceList,
                WeeklyChartData = weeklyChartData,
                DailyChartData = dailyChartData,
                CategoryChartData = categoryChartData
            };
        }
    }
}