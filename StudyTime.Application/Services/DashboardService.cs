using StudyTime.Application.DTOs.Dashboard;
using StudyTime.Application.Interfaces;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.Services
{
    public class DashboardService
    {
        private readonly ITaskRepository _taskRepository;

        public DashboardService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }
        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var tasks = await _taskRepository.GetAllAsync();
            var today = DateTime.Now;
            return new DashboardSummaryDto
            {
                TotalTasks = tasks.Count,
                PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
                CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),

                TodayTasks = tasks.Count(t =>
                t.StartDate.HasValue &&
                t.StartDate.Value.Date == today),

                TotalPlannedMinutes = tasks
                .Where(t => t.PlannedDuration.HasValue)
                 .Sum(t => (int)(t.PlannedDuration?.TotalMinutes ?? 0))

            };
        }
    }
}
