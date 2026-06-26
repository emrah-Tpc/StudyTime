using StudyTime.Application;
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
        ICurrentUserService currentUserService,
        ProductivityCalculator productivityCalculator)
    {
        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            // F11: Kullanıcı offset'i ile yerel "bugün"/gün hesabı (sunucu saat dilimi değil).
            var offset  = currentUserService.UtcOffsetMinutes;
            var nowUser = DateTime.UtcNow.AddMinutes(offset);
            DateTime ToUserLocal(DateTime utc) => utc.AddMinutes(offset);
            var today   = nowUser.Date;

            // ── Sequential Repository Calls (Thread-safe for DbContext) ─────────────────
            var viewRows = await dashboardRepository.GetDashboardSummariesAsync();
            var lessons = await lessonRepository.GetAllAsync() ?? new();
            


            // Optimization: Fetch only relevant tasks
            var todayTasksRaw = await taskRepository.GetByDateRangeAsync(today, today);
            var pendingTasksRaw = await taskRepository.GetPendingTasksAsync();

            // Combine today and pending tasks for internal logic
            var combinedTasks = todayTasksRaw.Concat(pendingTasksRaw).DistinctBy(t => t.Id).ToList();

            // Renk + durum için lessons
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
            // UTC offset kaymasını önlemek için today+1 gün çekip C#'ta local-time filtrele
            var rangeStart = today.AddDays(-14);
            var recentSessions = await studySessionRepository.GetByDateRangeAsync(rangeStart, today.AddDays(1));

            // FIX [Orta]: IsBreak filtresi tek noktada; tüm grafik hesapları workSessions'dan
            var workSessions = recentSessions.Where(s => !s.IsBreak).ToList();

            // FIX [Kritik]: Bugün dakikasını C# LINQ'dan ToLocalTime ile hesapla
            // GetByDateRangeAsync UTC filtresi timezone offset'i nedeniyle bugünü kaçırabilir;
            // burada local-time karşılaştırması ile kesin filtreleme yapılır.
            int todayMinutes = StudyDurationMetrics.SumChartMinutes(
                workSessions
                    .Where(s => ToUserLocal(s.StartedAt).Date == today)
                    .Select(s => s.CurrentDuration));

            // Geçen haftanın aynı günü vs bugün çalışma farkı (kullanıcı yerel günü)
            var lastWeekSameDay = today.AddDays(-7);
            var lastWeekSameDayMinutes = StudyDurationMetrics.SumChartMinutes(
                workSessions
                    .Where(s => ToUserLocal(s.StartedAt).Date == lastWeekSameDay)
                    .Select(s => s.CurrentDuration));
            int timeChange = todayMinutes - lastWeekSameDayMinutes;

            // Haftalık grafik (son 7 gün, mola hariç — workSessions'dan)
            var sessionByDate = workSessions
                .GroupBy(s => ToUserLocal(s.StartedAt).Date)
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
                        Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                            sessionByDate.GetValueOrDefault(date, 0) * 60)
                    };
                })
                .ToList();

            // Saatlik grafik (bugün, mola hariç — workSessions'dan)
            var sessionByHour = workSessions
                .Where(s => ToUserLocal(s.StartedAt).Date == today)
                .GroupBy(s => ToUserLocal(s.StartedAt).Hour)
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
                    Value = StudyDurationMetrics.ToChartMinutesFromTotalSeconds(
                        sessionByHour.GetValueOrDefault(h, 0) * 60)
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
                        Value = StudyDurationMetrics.SumChartMinutes(g.Select(s => s.CurrentDuration)),
                        Color = dominantLesson?.Color ?? "#6b7280"
                    };
                })
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            // ── 5. SON AKTİVİTELER + GÖREV İSTATİSTİKLERİ ───────────────────
            // Sadece bugünle alakalı olan görevleri al (Bugün oluşturulan, bugün biten veya bugün tamamlanan)
            var todayTasks = combinedTasks.Where(t =>
                (t.StartDate.HasValue && ToUserLocal(t.StartDate.Value).Date == today) ||
                (t.EndDate.HasValue && ToUserLocal(t.EndDate.Value).Date == today) ||
                (t.Status == AppTaskStatus.Completed && t.UpdatedAt.HasValue && ToUserLocal(t.UpdatedAt.Value).Date == today)
            ).ToList();

            int productivityScore = productivityCalculator.CalculateScore(
                workSessions.Where(s => ToUserLocal(s.StartedAt).Date == today),
                todayTasks,
                today,
                today.AddDays(1).AddTicks(-1));

            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek  = today.AddDays(-diff).Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            int tasksCreatedThisWeek = combinedTasks.Count(t => ToUserLocal(t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            int completedThisWeek    = combinedTasks.Count(t => t.Status == AppTaskStatus.Completed && ToUserLocal(t.StartDate ?? DateTime.MinValue).Date >= startOfWeek);
            int completedThisMonth   = combinedTasks.Count(t => t.Status == AppTaskStatus.Completed && ToUserLocal(t.StartDate ?? DateTime.MinValue).Date >= startOfMonth);
            int cancelledTasks       = combinedTasks.Count(t => t.Status == AppTaskStatus.Cancelled);

            var recentActivities = combinedTasks
                .Where(t => t.StartDate.HasValue)
                .OrderByDescending(t => t.StartDate)
                .Take(4)
                .Select(t =>
                {
                    bool isCompleted = t.Status == AppTaskStatus.Completed;
                    bool isPending   = t.Status == AppTaskStatus.Pending;
                    var  timeSpan    = DateTime.UtcNow - t.StartDate!.Value;

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
                CancelledTasks       = cancelledTasks,
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