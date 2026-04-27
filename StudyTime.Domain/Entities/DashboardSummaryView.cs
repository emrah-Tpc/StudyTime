namespace StudyTime.Domain.Entities
{
    public class DashboardSummaryView
    {
        public Guid LessonId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public required string LessonName { get; set; } // 👈 'required' eklendi
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int TotalStudyMinutes { get; set; }
        public int TodayStudyMinutes { get; set; }
    }
}