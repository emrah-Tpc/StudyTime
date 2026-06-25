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
                .Where(s => {
                    var localStart = s.StartedAt.Kind == DateTimeKind.Utc ? s.StartedAt.ToLocalTime() : s.StartedAt;
                    return localStart.Date >= from.Date && localStart.Date <= to.Date;
                })
                .Sum(s => s.TotalActiveDuration.TotalMinutes);

            var completedTasks = tasks.Count(t => t.Status == DomainTaskStatus.Completed);
            var totalTasks = tasks.Count(t => t.Status != DomainTaskStatus.Cancelled);

            // Maksimum 4 saat hedefi = 240 dakika
            var timeScore = Math.Min(totalTime / 240.0 * 100.0, 100.0);

            // Görev yoksa: Puan sadece çalışma süresine dayanır (Maksimum %100 olabilir)
            if (totalTasks == 0) 
            {
                return (int)timeScore;
            }
            
            // Görev varsa: %50 Görev Başarısı + %50 Çalışma Süresi (Daha dengeli)
            var taskScore = (double)completedTasks / totalTasks * 100.0;
            return (int)((taskScore * 0.5) + (timeScore * 0.5));
        }
    }
}
