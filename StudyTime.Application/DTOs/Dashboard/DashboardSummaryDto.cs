using System.Collections.Generic;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class DashboardSummaryDto
    {
        // Temel Sayaçlar
        public int TotalTasks { get; set; }
        public int TasksCreatedThisWeek { get; set; }

        public int PendingTasks { get; set; }
        public int HighPriorityPending { get; set; }

        public int CompletedTasks { get; set; }
        public int CompletionRate { get; set; }

        public int TodayStudiedMinutes { get; set; }
        public int StudyTimeChange { get; set; }

        public int CancelledTasks { get; set; }
        public int ActiveLessons { get; set; }
        public int ProductivityScore { get; set; }

        // Alt Bilgiler
        public int CompletedThisWeek { get; set; }
        public int CompletedThisMonth { get; set; }

        // Listeler
        public List<DashboardWorkspaceDto> Workspaces { get; set; } = new();

        // 👇 YENİ: Günlük (Daily) sekmesi için veriler
        public List<ChartDataDto> DailyChartData { get; set; } = new();

        public List<ChartDataDto> WeeklyChartData { get; set; } = new();
        public List<ChartDataDto> CategoryChartData { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
    }

    public sealed class DashboardWorkspaceDto
    {
        public Guid LessonId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#38bdf8";
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int ProgressPercent { get; set; }
        public string TotalTimeTracked { get; set; } = "0m";
    }

    public class RecentActivityDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string StatusColorClass { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
    }
}