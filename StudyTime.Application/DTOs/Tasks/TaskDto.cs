using StudyTime.Domain.Enums;
using System;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.DTOs.Tasks
{
    public class TaskDto
    {
        public Guid Id { get; set; }
        public Guid? LessonId { get; set; }
        public string Title { get; set; } = default!;
        public string? Note { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public TimeSpan? PlannedDuration { get; set; }

        // 👇 String yerine Enum yaptık
        public TaskStatus Status { get; set; }
    }
}