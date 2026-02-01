using System.Net.Http.Json;
using StudyTime.Application.DTOs.Dashboard;

namespace StudyTime.DesktopClient.Services
{
    public class DashboardApiService
    {
        private readonly HttpClient _http;

        public DashboardApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<DashboardSummaryDto?> GetSummaryAsync()
        {
            return await _http.GetFromJsonAsync<DashboardSummaryDto>(
                "/api/dashboard/summary");
        }
    }
}
