using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class DashboardSummaryDto
    {
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CancelledTasks { get; set; }
        public int TodayTasks { get; set; }
        public int TotalPlannedMinutes { get; set; }

    }
}
