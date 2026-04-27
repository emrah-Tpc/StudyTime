using System.Net.Http.Json;
using StudyTime.Application.DTOs.Dashboard;

namespace StudyTime.DesktopClient.Services
{
    public class DashboardApiService
    {
        private readonly HttpClient http;

        public DashboardApiService(IHttpClientFactory factory)
        {
            this.http = factory.CreateClient("StudyTimeApi");
        }

        public async Task<DashboardSummaryDto?> GetSummaryAsync()
        {
            return await http.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");
        }
    }
}