namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class DashboardSummaryDto
    {
        // --- TASKS ---
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CancelledTasks { get; set; }

        // --- LESSONS ---
        public int ActiveLessons { get; set; }

        // --- TIME ---
        public int TodayStudiedMinutes { get; set; }

        // --- PRODUCTIVITY ---
        public int ProductivityScore { get; set; }
    }
}
