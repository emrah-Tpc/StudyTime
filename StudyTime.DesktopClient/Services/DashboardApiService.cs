using System.Net.Http.Json;
using StudyTime.Application.DTOs.Dashboard;

namespace StudyTime.DesktopClient.Services
{
    // 👇 Primary Constructor (Sınıf isminin yanına parantez açtık)
    public class DashboardApiService(HttpClient http)
    {
        public async Task<DashboardSummaryDto?> GetSummaryAsync()
        {
            return await http.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");
        }
    }
}