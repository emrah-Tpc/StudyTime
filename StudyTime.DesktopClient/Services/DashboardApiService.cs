using StudyTime.Application.DTOs.Dashboard;

namespace StudyTime.DesktopClient.Services;

public sealed class DashboardApiService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        // TEMP: Backend yokken fake data
        await Task.Delay(600);

        return new DashboardSummaryDto
        {
            ProductivityScore = 66,
            ActiveLessons = 2,
            PendingTasks = 1,
            CompletedTasks = 2,
            TodayStudiedMinutes = 0
        };
    }
}
