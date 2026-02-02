using StudyTime.Domain.Enums; // Enum'ları kullanmak için ekle
using System;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;
namespace StudyTime.Application.DTOs.Tasks
{
    public sealed class TaskListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;

        // String yerine Enum kullanıyoruz, LessonListItemDto ile tutarlı olsun
        public TaskStatus Status { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // UI için dakika cinsinden süre
        public int? PlannedDurationMinutes { get; set; }

        // İlişkili Ders Bilgileri (UI'da göstermek için şart)
        public Guid? LessonId { get; set; }
        public string? LessonName { get; set; }
        public string? LessonColor { get; set; }
    }
}