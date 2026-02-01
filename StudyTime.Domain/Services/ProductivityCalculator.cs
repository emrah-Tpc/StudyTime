using StudyTime.Domain.Entities;
using DomainTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Domain.Services
{
    public class ProductivityCalculator
    {
        public static int CalculateScore(
            IEnumerable<StudySession> sessions,
            IEnumerable<TaskItem> tasks,
            DateTime from,
            DateTime to)
        {
            var totalTime = sessions
                .Where(s => s.StartedAt >= from && s.StartedAt <= to)
                .Sum(s => s.TotalActiveDuration.TotalMinutes);

            var completedTasks = tasks.Count(t => t.Status == DomainTaskStatus.Completed);
            var totalTasks = tasks.Count(t => t.Status != DomainTaskStatus.Cancelled);

            if (totalTasks == 0) return 0;

            var taskScore = (double)completedTasks / totalTasks * 100;
            var timeScore = Math.Min(totalTime / 240 * 100, 100); // 4 saat = max

            return (int)((taskScore * 0.6) + (timeScore * 0.4));
        }
    }
}
