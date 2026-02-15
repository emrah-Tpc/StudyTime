using StudyTime.Application.DTOs.Statistics;

namespace StudyTime.Application.Interfaces
{
    public interface IStatisticsService
    {
        Task<StatisticsSummaryDto> GetStatisticsAsync(DateTime startDate, DateTime endDate);
    }
}
