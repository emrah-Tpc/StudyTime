using StudyTime.Domain.Enums;
using System;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class WorkspaceSummaryDto
    {
        public LessonType Type { get; init; }

        public int TotalTasks { get; init; }
        public int CompletedTasks { get; init; }
        public int PendingTasks { get; init; }

        public int ProgressPercentage { get; init; }
        public TimeSpan TotalTrackedTime { get; init; }
    }
}
