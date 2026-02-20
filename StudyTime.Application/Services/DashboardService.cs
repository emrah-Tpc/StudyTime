using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;
using System.Globalization;
using AppTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class DashboardService(
        IDashboardRepository dashboardRepository,
        ILessonRepository lessonRepository,
        IStudySessionRepository studySessionRepository,
        ITaskRepository taskRepository)
    {
        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var today     = DateTime.Today;
            var yesterday = today.AddDays(-1);

            // ── 1. VIEW: Ders bazlı özetler (tek SQL sorgusu) ─────────────────
            var viewRows = await dashboardRepository.GetDashboardSummariesAsync();

            // Renk + durum için lessons (sadece Id, Color, Status projection'ı)
            var lessons = await lessonRepository.GetAllAsync() ?? new();
            var activeLessons = lessons
                .Where(l => l.Status == LessonStatus.Active && !l.IsDeleted)
                .ToList();
            var activeLessonIds = activeLessons.Select(l => l.Id).ToHashSet();
            var colorMap = activeLessons.ToDictionary(l => l.Id, l => l.Color ?? "#3b82f6");

            // ── 2. AGREGATİF TOPLAMLAR (view'dan) ────────────────────────────
            int totalTasks     = viewRows.Sum(r => r.TotalTasks);
            int completedTasks = viewRows.Sum(r => r.CompletedTasks);
            int pendingTasks   = totalTasks - completedTasks;
            int todayMinutes   = viewRows.Sum(r => r.TodayStudyMinutes);
            int completionRate = totalTasks == 0 ? 0
                : (int)Math.Round((double)completedTasks / totalTasks * 100);

            // ── 3. WORKSPACES (view satırlarından, sadece aktif dersler) ──────
            var workspaceList = viewRows
                .Where(r => activeLessonIds.Contains(r.LessonId))
                .Select(r =>
                {
                    int lTotal     = r.TotalTasks;
                    int lCompleted = r.CompletedTasks;
                    int progress   = lTotal == 0 ? 0
                        : (int)Math.Round((double)lCompleted / lTotal * 100);

                    double totalMin = r.TotalStudyMinutes;
                    string timeStr  = totalMin < 60
                        ? $"{Math.Ceiling(totalMin)}m"
                        : $"{Math.Round(totalMin / 60.0, 1)}h";

                    return new DashboardWorkspaceDto
                    {
                        LessonId         = r.LessonId,
                        Name             = r.LessonName,
                        Color            = colorMap.GetValueOrDefault(r.LessonId, "#3b82f6"),
                        TotalTasks       = lTotal,
                        CompletedTasks   = lCompleted,
                        PendingTasks     = lTotal - lCompleted,
                        ProgressPercent  = progress,
                        TotalTimeTracked = timeStr
                    };
                })
                .OrderByDescending(w => w.CompletedTasks)
                .ToList();

            // ── 4. DİNAMİK VERİLER: haftalık/günlük/kategori grafikleri ──────
            //    (View'da tarih bazlı breakdown yok → session repository kullanılır)
            var allSessions = await studySessionRepository.GetAllAsync() ?? new();

            // Dün vs bugün çalışma farkı
            var yesterdayMinutes = allSessions
                .Where(s => s.StartedAt.Date == yesterday)
                .Sum(s => (int)s.CurrentDuration.TotalMinutes);
            int timeChange = todayMinutes - yesterdayMinutes;

            // Verimlilik Skoru
            int productivityScore = 0;
            if (totalTasks > 0 || todayMinutes > 0)
            {
                int taskScore = completionRate;
                int timeScore = Math.Min(todayMinutes * 100 / 240, 100);
                productivityScore = (int)(taskScore * 0.6 + timeScore * 0.4);
            }

            // Haftalık grafik (son 7 gün, mola hariç)
            var sessionByDate = allSessions
                .Where(s => !s.IsBreak)
                .GroupBy(s => s.StartedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => (int)s.CurrentDuration.TotalMinutes));

            var weeklyChartData = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date    = today.AddDays(-(6 - i));
                    var dayName = date.ToString("ddd", new CultureInfo("tr-TR"));
                    return new ChartDataDto
                    {
                        Label = dayName,
                        Value = sessionByDate.GetValueOrDefault(date, 0)
                    };
                })
                .ToList();

            // Saatlik grafik (bugün 00-23, mola hariç)
            var todaysSessions = allSessions
                .Where(s => s.StartedAt.Date == today && !s.IsBreak)
                .ToList();
            var sessionByHour = todaysSessions
                .GroupBy(s => s.StartedAt.Hour)
                .ToDictionary(g => g.Key, g => g.Sum(s => (int)s.CurrentDuration.TotalMinutes));
            var dailyChartData = Enumerable.Range(0, 24)
                .Select(h => new ChartDataDto
                {
                    Label = $"{h:D2}:00",
                    Value = sessionByHour.GetValueOrDefault(h, 0)
                })
                .ToList();

            // Kategori grafiği (Lesson.Type gruplandırması)
            var categoryChartData = allSessions
                .Where(s => s.LessonId != Guid.Empty && s.Lesson != null)
                .GroupBy(s => s.Lesson!.Type)
                .Select(g =>
                {
                    var typeName = g.Key switch
                    {
                        LessonType.Academic => "Okul",
                        LessonType.Personal => "Kişisel",
                        LessonType.Work     => "İş",
                        _                   => g.Key.ToString()
                    };
                    var dominantLesson = g
                        .GroupBy(x => x.LessonId)
                        .OrderByDescending(sub => sub.Sum(x => x.CurrentDuration.TotalMinutes))
                        .FirstOrDefault()?.FirstOrDefault()?.Lesson;

                    return new ChartDataDto
                    {
                        Label = typeName,
                        Value = (int)g.Sum(s => s.CurrentDuration.TotalMinutes),
                        Color = dominantLesson?.Color ?? "#6b7280"
                    };
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            // ── 5. SON AKTİVİTELER + GÖREV İSTATİSTİKLERİ ───────────────────
            var tasks = await taskRepository.GetAllAsync() ?? new();

            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek  = today.AddDays(-diff).Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            int tasksCreatedThisWeek = tasks.Count(t => (t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            int completedThisWeek    = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            int completedThisMonth   = tasks.Count(t => t.Status == AppTaskStatus.Completed && (t.StartDate ?? DateTime.MinValue).Date >= startOfMonth);

            var recentActivities = tasks
                .Where(t => t.StartDate.HasValue)
                .OrderByDescending(t => t.StartDate)
                .Take(4)
                .Select(t =>
                {
                    bool isCompleted = t.Status == AppTaskStatus.Completed;
                    bool isPending   = t.Status == AppTaskStatus.Pending;
                    var  timeSpan    = DateTime.Now - t.StartDate!.Value;

                    string timeAgo = timeSpan.TotalMinutes < 60 ? $"{Math.Max(1, Math.Ceiling(timeSpan.TotalMinutes))}dk önce"
                                   : timeSpan.TotalHours   < 24 ? $"{Math.Ceiling(timeSpan.TotalHours)}s önce"
                                   : $"{Math.Ceiling(timeSpan.TotalDays)} gün önce";

                    return new RecentActivityDto
                    {
                        Id               = t.Id,
                        IsCompleted      = isCompleted,
                        Title            = t.Title,
                        Subtitle         = $"{t.Lesson?.Name ?? "Genel"} • {timeAgo}",
                        StatusText       = isCompleted ? "Tamamlandı" : (isPending ? "Sürüyor" : "Bekliyor"),
                        StatusColorClass = isCompleted ? "text-success" : (isPending ? "text-primary" : "text-warning"),
                        IconClass        = isCompleted ? "bi-check-lg" : (isPending ? "bi-play-fill" : "bi-clock")
                    };
                })
                .ToList();

            // ── 6. DTO DÖNÜŞÜ ─────────────────────────────────────────────────
            return new DashboardSummaryDto
            {
                TotalTasks           = totalTasks,
                TasksCreatedThisWeek = tasksCreatedThisWeek,
                PendingTasks         = pendingTasks,
                CompletedTasks       = completedTasks,
                CompletionRate       = completionRate,
                TodayStudiedMinutes  = todayMinutes,
                StudyTimeChange      = timeChange,
                ProductivityScore    = productivityScore,
                CompletedThisWeek    = completedThisWeek,
                CompletedThisMonth   = completedThisMonth,
                ActiveLessons        = activeLessons.Count,
                RecentActivities     = recentActivities,
                Workspaces           = workspaceList,
                WeeklyChartData      = weeklyChartData,
                DailyChartData       = dailyChartData,
                CategoryChartData    = categoryChartData
            };
        }
    }
}