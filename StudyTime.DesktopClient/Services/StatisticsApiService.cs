using System.Net.Http.Json;
using StudyTime.Application.DTOs.Statistics;

namespace StudyTime.DesktopClient.Services
{
    public class StatisticsApiService(HttpClient http)
    {
        public async Task<StatisticsSummaryDto?> GetStatisticsAsync(string range)
        {
            return await http.GetFromJsonAsync<StatisticsSummaryDto>($"/api/statistics?range={range}");
        }
    }
}
