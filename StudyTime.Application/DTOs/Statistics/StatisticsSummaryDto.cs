namespace StudyTime.Application.DTOs.Statistics
{
    public class StatisticsSummaryDto
    {
        public TimeSpan TotalStudyTime { get; set; }
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

    public class TimeTrendDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class ProductivityDto
    {
        public int Hour { get; set; } // 0-23
        public double Score { get; set; } // Minutes studied in this hour
    }
}
