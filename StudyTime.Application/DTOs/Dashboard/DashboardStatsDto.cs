using System;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class DashboardStatsDto
    {
        public int ActiveLessons { get; init; }
        public int PendingTasks { get; init; }
        public int CompletedTasks { get; init; }
        public TimeSpan TotalTrackedTime { get; init; }
    }
}
