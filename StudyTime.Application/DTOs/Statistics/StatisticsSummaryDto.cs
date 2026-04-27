namespace StudyTime.Application.DTOs.Statistics
{
    public class StatisticsSummaryDto
    {
        public TimeSpan TotalStudyTime { get; set; }
        public TimeSpan TotalBreakTime  { get; set; }  // Mola oturumları ayrı tutulur
        public double AverageDailyStudyMinutes { get; set; }
        public int TotalTasksCompleted { get; set; }
        public int ProductivityScore { get; set; }
        public List<LessonStatisticDto> LessonStatistics { get; set; } = new();
        public List<TaskStatisticDto> TaskStatistics { get; set; } = new();
        public List<TimeTrendDto> StudyTrends { get; set; } = new();
        public List<ProductivityDto> PeakProductivity { get; set; } = new();

        // New Metrics for Refined UI
        public string MostProductiveDay { get; set; } = "-";
        public double AverageSessionDuration { get; set; }
        public int TotalSessions { get; set; }

        // Pomodoro / Günlük Seans Heatmap
        public List<DailySessionDto> DailySessionCounts { get; set; } = new();
    }

    public class LessonStatisticDto
    {
        public string LessonName { get; set; } = string.Empty;
        public string Color { get; set; } = "#3b82f6";
        public double TotalDurationMinutes { get; set; }
        public int TaskCompletionRate { get; set; }
    }

    public class TaskStatisticDto
    {
        public string Title { get; set; } = string.Empty;
        public string LessonName { get; set; } = string.Empty;
        public double DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>Bir günde tamamlanan seans sayısı (heatmap için).</summary>
    public class DailySessionDto
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        /// <summary>En çok çalışılan ders rengi (UI renklendirme için).</summary>
        public string DominantColor { get; set; } = "#3b82f6";
    }

    public class TimeTrendDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class ProductivityDto
    {
        public int Hour { get; set; } // 0-23 (Start of the block)
        public double Score { get; set; } // Total minutes studied in this 3-hour block
        public string Label { get; set; } = string.Empty; // e.g. "14:00-17:00"
        public bool IsPeakRange { get; set; } // True if this is the highest block
    }
}
