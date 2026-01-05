namespace StudyTime.Application.DTOs.Tasks
{
    public sealed class CreateTaskDto
    {
        public string Title { get; set; } = default!;
        public Guid? LessonId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Note { get; set; }

        // UI'dan dakika olarak gelecek, backend'de TimeSpan'a çevireceğiz
        public int? PlannedDurationMinutes { get; set; }
    }
}