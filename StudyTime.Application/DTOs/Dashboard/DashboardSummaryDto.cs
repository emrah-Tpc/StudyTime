using System.Collections.Generic;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class DashboardSummaryDto
    {
        // Temel Sayaçlar
        public int TotalTasks { get; set; }
        public int TasksCreatedThisWeek { get; set; } // YENİ: Bu hafta eklenen görev sayısı

        public int PendingTasks { get; set; }
        public int HighPriorityPending { get; set; } // YENİ: Öncelikli/Acil görevler

        public int CompletedTasks { get; set; }
        public int CompletionRate { get; set; } // YENİ: Başarı oranı (%)

        public int TodayStudiedMinutes { get; set; }
        public int StudyTimeChange { get; set; } // YENİ: Düne göre değişim (dk)

        public int CancelledTasks { get; set; }
        public int ActiveLessons { get; set; }
        public int ProductivityScore { get; set; }

        // Alt Bilgiler
        public int CompletedThisWeek { get; set; }
        public int CompletedThisMonth { get; set; }

        // Listeler
        public List<DashboardWorkspaceDto> Workspaces { get; set; } = new();
        public List<ChartDataDto> WeeklyChartData { get; set; } = new();
        public List<ChartDataDto> CategoryChartData { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
    }

    // ... Diğer sınıflar (DashboardWorkspaceDto vb.) aynı kalabilir ...
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