using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;
using StudyTime.Domain.Services;
using System.Globalization;
using AppTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class DashboardService(
        IDashboardRepository dashboardRepository,
        ILessonRepository lessonRepository,
        IStudySessionRepository studySessionRepository,
        ITaskRepository taskRepository,
        ProductivityCalculator productivityCalculator)
    {
        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var today     = DateTime.Today;

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

            // ── 4. DİNAMİK VERİLER ───────────────────────────────────────────
            // FIX [Kritik]: GetAllAsync() yerine son 14 günlük veri çek
            // Haftalık (7 gün) + önceki hafta karşılaştırması (7 gün) = 14 gün yeterli
            var rangeStart     = today.AddDays(-13); // 14 gün (bugün dahil)
            var recentSessions = await studySessionRepository.GetByDateRangeAsync(rangeStart, today);

            // FIX [Orta]: IsBreak filtresi tek noktada; tüm grafik hesapları workSessions'dan
            var workSessions = recentSessions.Where(s => !s.IsBreak).ToList();

            // FIX [Kritik]: Bugün dakikasını SQL view yerine C# LINQ'dan hesapla
            // → Bugün kartı + grafikler aynı kaynaktan, tutarlı
            // FIX: C# LINQ'dan günlük takibi hassas hesapla
            int todayMinutes = (int)Math.Round(workSessions
                .Where(s => s.StartedAt.Date == today)
                .Sum(s => s.CurrentDuration.TotalMinutes));

            // Geçen haftanın aynı günü vs bugün çalışma farkı
            var lastWeekSameDay        = today.AddDays(-7);
            var lastWeekSameDayMinutes = (int)Math.Round(workSessions
                .Where(s => s.StartedAt.Date == lastWeekSameDay)
                .Sum(s => s.CurrentDuration.TotalMinutes));
            int timeChange = todayMinutes - lastWeekSameDayMinutes;

            // Haftalık grafik (son 7 gün, mola hariç — workSessions'dan)
            var sessionByDate = workSessions
                .GroupBy(s => s.StartedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            // FIX [Minör]: Label'a gün adı + tarih numarası eklendi ("Pzt 28")
            var weeklyChartData = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date    = today.AddDays(-(6 - i));
                    var dayName = date.ToString("ddd", new CultureInfo("tr-TR"));
                    return new ChartDataDto
                    {
                        Label = $"{dayName} {date.Day}",
                        Value = (int)Math.Round(sessionByDate.GetValueOrDefault(date, 0))
                    };
                })
                .ToList();

            // Saatlik grafik (bugün, mola hariç — workSessions'dan)
            var sessionByHour = workSessions
                .Where(s => s.StartedAt.Date == today)
                .GroupBy(s => s.StartedAt.Hour)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.CurrentDuration.TotalMinutes));

            // FIX [Minör]: Anlamlı saat aralığı — hiç çalışılmadıysa 08-20, çalışıldıysa ±1 saat pad
            int firstHour, lastHour;
            if (sessionByHour.Any())
            {
                firstHour = Math.Max(0,  sessionByHour.Keys.Min() - 1);
                lastHour  = Math.Min(23, sessionByHour.Keys.Max() + 1);
            }
            else
            {
                firstHour = 8; lastHour = 20;
            }

            var dailyChartData = Enumerable.Range(firstHour, lastHour - firstHour + 1)
                .Select(h => new ChartDataDto
                {
                    Label = $"{h:D2}:00",
                    Value = (int)Math.Round(sessionByHour.GetValueOrDefault(h, 0))
                })
                .ToList();

            // FIX [Orta]: Kategori grafiği — IsBreak zaten workSessions ile filtrelendi ✓
            var categoryChartData = workSessions
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
                        Value = (int)Math.Round(g.Sum(s => s.CurrentDuration.TotalMinutes)),
                        Color = dominantLesson?.Color ?? "#6b7280"
                    };
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            // ── 5. SON AKTİVİTELER + GÖREV İSTATİSTİKLERİ ───────────────────
            var tasks = await taskRepository.GetAllAsync() ?? new();

            // FIX [Kritik]: Yamayı kaldırdık. Yalnızca bugünün görevlerini kullanarak doğru tutarlılığı sağlıyoruz.
            var todayTasks   = tasks.Where(t => t.StartDate?.Date == today).ToList();

            int productivityScore = productivityCalculator.CalculateScore(
                workSessions.Where(s => s.StartedAt.Date == today),
                todayTasks,
                today,
                today.AddDays(1).AddTicks(-1));

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