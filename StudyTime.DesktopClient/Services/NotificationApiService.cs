using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using StudyTime.Domain.Entities;

namespace StudyTime.DesktopClient.Services
{
    public class NotificationApiService
    {
        private readonly HttpClient _http;

        public NotificationApiService(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("StudyTimeApi");
        }

        public async Task<List<Notification>> GetNotificationsAsync()
        {
            var response = await _http.GetFromJsonAsync<List<Notification>>("api/notification");
            return response ?? new List<Notification>();
        }

        /// <summary>
        /// Sunucuda yeni bildirim oluşturur ve sunucunun atadığı Id'yi döner.
        /// </summary>
        public async Task<Guid> CreateAsync(Notification notification)
        {
            var response = await _http.PostAsJsonAsync("api/notification", notification);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<Notification>();
            return created?.Id ?? Guid.Empty;
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            await _http.PutAsync($"api/notification/{id}/read", null);
        }

        public async Task MarkAllAsReadAsync()
        {
            await _http.PutAsync("api/notification/read-all", null);
        }

        public async Task DeleteAllAsync()
        {
            await _http.DeleteAsync("api/notification/all");
        }
    }
}
