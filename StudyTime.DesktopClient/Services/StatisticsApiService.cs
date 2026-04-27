using System.Net.Http.Json;
using StudyTime.Application.DTOs.Statistics;

namespace StudyTime.DesktopClient.Services
{
    public class StatisticsApiService
    {
        private readonly HttpClient http;

        public StatisticsApiService(IHttpClientFactory factory)
        {
            this.http = factory.CreateClient("StudyTimeApi");
        }

        public async Task<StatisticsSummaryDto?> GetStatisticsAsync(string range)
        {
            return await http.GetFromJsonAsync<StatisticsSummaryDto>($"/api/statistics?range={range}");
        }
    }
}
