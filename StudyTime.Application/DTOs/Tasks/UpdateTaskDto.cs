using StudyTime.Domain.Enums;
using System;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;
namespace StudyTime.Application.DTOs.Tasks
{
    public sealed class UpdateTaskDto
    {
        public string Title { get; set; } = default!;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Note { get; set; }
        public int? PlannedDurationMinutes { get; set; }

        // Görevi güncellerken durumunu da değiştirebilmek için
        public TaskStatus Status { get; set; }

        public Guid? LessonId { get; set; }
    }
}