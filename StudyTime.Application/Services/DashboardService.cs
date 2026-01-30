using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;

namespace StudyTime.Application.Services
{
    public class DashboardService(
        ITaskRepository taskRepository,
        ILessonRepository lessonRepository,
        IStudySessionRepository studySessionRepository)
    {
        private readonly ITaskRepository _taskRepository = taskRepository;
        private readonly ILessonRepository _lessonRepository = lessonRepository;
        private readonly IStudySessionRepository _studySessionRepository = studySessionRepository;

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            // 1. Tüm verileri çek (Gerçek projede burası daha optimize yazılabilir)
            var tasks = await _taskRepository.GetAllAsync();
            var lessons = await _lessonRepository.GetAllAsync();
            var todaySessions = await _studySessionRepository.GetByDateAsync(DateTime.Today);
            // Tüm sessionları çekmemiz lazım ki dersin toplam süresini hesaplayalım
            var allSessions = await _studySessionRepository.GetAllAsync();

            // 2. Basit Hesaplamalar
            var todayMinutes = todaySessions.Sum(s => (int)s.CurrentDuration.TotalMinutes);
            var completedCount = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
            var totalCount = tasks.Count;
            var activeLessons = lessons.Where(l => l.Status == LessonStatus.Active && !l.IsDeleted).ToList();

            // 3. Workspace (Ders Kartları) Listesini Oluşturma 🚀
            var workspaceList = new List<DashboardWorkspaceDto>();

            foreach (var lesson in activeLessons)
            {
                // Bu derse ait görevler
                var lessonTasks = tasks.Where(t => t.LessonId == lesson.Id).ToList();
                int lTotal = lessonTasks.Count;
                int lCompleted = lessonTasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
                int lPending = lessonTasks.Count(t => t.Status == Domain.Enums.TaskStatus.Pending);

                // İlerleme Yüzdesi
                int progress = lTotal == 0 ? 0 : (int)((double)lCompleted / lTotal * 100);

                // Bu ders için toplam çalışma süresi
                var lessonSessions = allSessions.Where(s => s.LessonId == lesson.Id);
                double totalMinutes = lessonSessions.Sum(s => s.CurrentDuration.TotalMinutes);
                string timeStr = totalMinutes < 60
                    ? $"{Math.Ceiling(totalMinutes)}m"
                    : $"{Math.Round(totalMinutes / 60, 1)}h";

                workspaceList.Add(new DashboardWorkspaceDto
                {
                    LessonId = lesson.Id,
                    Name = lesson.Name,
                    Color = lesson.Color, // Dersin kendi rengini kullanıyoruz
                    TotalTasks = lTotal,
                    CompletedTasks = lCompleted,
                    PendingTasks = lPending,
                    ProgressPercent = progress,
                    TotalTimeTracked = timeStr
                });
            }

            return new DashboardSummaryDto
            {
                TotalTasks = totalCount,
                PendingTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Pending),
                CompletedTasks = completedCount,
                CancelledTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Cancelled),
                ActiveLessons = activeLessons.Count,
                TodayStudiedMinutes = todayMinutes,
                ProductivityScore = totalCount == 0 ? 0 : (int)((double)completedCount / totalCount * 100),

                // Listeyi ekle
                Workspaces = workspaceList
            };
        }
    }
}
