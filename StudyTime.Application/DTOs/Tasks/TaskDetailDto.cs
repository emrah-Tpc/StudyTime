using StudyTime.Domain.Enums; // Enum için gerekli
using System;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Application.DTOs.Tasks
{
    public sealed class TaskDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Note { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? PlannedDurationMinutes { get; set; }

        // String yerine Enum
        public TaskStatus Status { get; set; }

        public Guid? LessonId { get; set; }

        // 👇 EKSİK OLAN ALANLAR BUNLARDI
        public string? LessonName { get; set; }
        public string? LessonColor { get; set; }
    }
}