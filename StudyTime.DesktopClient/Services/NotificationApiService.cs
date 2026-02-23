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

        public NotificationApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Notification>> GetNotificationsAsync()
        {
            var response = await _http.GetFromJsonAsync<List<Notification>>("api/notification");
            return response ?? new List<Notification>();
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            await _http.PutAsync($"api/notification/{id}/read", null);
        }

        public async Task MarkAllAsReadAsync()
        {
            await _http.PutAsync("api/notification/read-all", null);
        }
    }
}
