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

        // 👇 YENİ: Carousel için detaylı liste
        public List<DashboardWorkspaceDto> Workspaces { get; set; } = new();
    }

    // Carousel kartının içindeki veriler için yeni DTO
    public sealed class DashboardWorkspaceDto
    {
        public int LessonId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#38bdf8";
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int ProgressPercent { get; set; }
        public string TotalTimeTracked { get; set; } = "0h";
    }
}