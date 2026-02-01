using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Enums;

namespace StudyTime.Application.Services
{
    public class DashboardService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ILessonRepository _lessonRepository;
        private readonly IStudySessionRepository _studySessionRepository;

        public DashboardService(
            ITaskRepository taskRepository,
            ILessonRepository lessonRepository,
            IStudySessionRepository studySessionRepository)
        {
            _taskRepository = taskRepository;
            _lessonRepository = lessonRepository;
            _studySessionRepository = studySessionRepository;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var tasks = await _taskRepository.GetAllAsync();
            var lessons = await _lessonRepository.GetAllAsync();
            var todaySessions = await _studySessionRepository.GetByDateAsync(DateTime.Today);

            var todayMinutes = todaySessions
                .Sum(s => (int)s.CurrentDuration.TotalMinutes);

            var completed = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
            var total = tasks.Count;

            return new DashboardSummaryDto
            {
                TotalTasks = total,
                PendingTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Pending),
                CompletedTasks = completed,
                CancelledTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Cancelled),

                ActiveLessons = lessons.Count(l => l.Status == LessonStatus.Active && !l.IsDeleted),

                TodayStudiedMinutes = todayMinutes,

                ProductivityScore = total == 0
                    ? 0
                    : (int)((double)completed / total * 100)
            };
        }
    }
}
