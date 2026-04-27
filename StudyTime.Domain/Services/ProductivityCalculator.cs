using StudyTime.Domain.Entities;
using DomainTaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Domain.Services
{
    public class ProductivityCalculator
    {
        public int CalculateScore(
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

            // Maksimum 4 saat hedefi = 240 dakika
            var timeScore = Math.Min(totalTime / 240.0 * 100.0, 100.0);

            // Görev yoksa: Max verimlilik puanı timeScore üzerinden, ancak %50 limite takılır. (Teşvik için)
            if (totalTasks == 0) 
            {
                return (int)(timeScore * 0.50);
            }

            // Görev varsa formül: %60 Görev Başarısı + %40 Süre
            var taskScore = (double)completedTasks / totalTasks * 100.0;
            return (int)((taskScore * 0.6) + (timeScore * 0.4));
        }
    }
}
