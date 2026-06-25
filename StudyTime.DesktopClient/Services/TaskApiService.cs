using System.Net.Http.Json;
using System.Text.Json;
using StudyTime.Application.DTOs.Tasks;

namespace StudyTime.DesktopClient.Services
{
    public class TaskApiService
    {
        private readonly HttpClient _httpClient;

        public TaskApiService(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient("StudyTimeApi");
        }

        // Görevleri Getir
        public async Task<List<TaskDto>> GetTasksByLessonIdAsync(Guid lessonId)
        {
            try
            {
                var tasks = await _httpClient.GetFromJsonAsync<List<TaskDto>>("api/tasks");
                if (tasks == null) return new List<TaskDto>();

                if (lessonId == Guid.Empty)
                {
                    return tasks.Where(t => t.LessonId == null).ToList();
                }

                return tasks.Where(t => t.LessonId == lessonId).ToList();
            }
            catch
            {
                return new List<TaskDto>();
            }
        }

        public async Task<List<TaskDto>> GetTasksByDateRangeAsync(DateTime start, DateTime end)
        {
            var startStr = start.ToString("yyyy-MM-ddTHH:mm:ss");
            var endStr = end.ToString("yyyy-MM-ddTHH:mm:ss");
            return await _httpClient.GetFromJsonAsync<List<TaskDto>>($"api/tasks/range?start={startStr}&end={endStr}")
                   ?? new List<TaskDto>();
        }

        public async Task UpdateAsync(Guid id, UpdateTaskDto dto)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Güncellenemedi: {error}");
            }
        }

        /// <summary>Sunucunun atadığı görev Id'sini döndürür (outbox reconcilation).</summary>
        public async Task<Guid> CreateAsync(CreateTaskDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/tasks", dto);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"API Hatası: [Status: {response.StatusCode}] Detay: {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("taskId", out var idEl))
                throw new Exception("Yanıtta taskId yok.");
            return idEl.GetGuid();
        }

        public async Task ToggleCompleteAsync(Guid id, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? $"?updatedAt={updatedAt.Value:O}" : "";
            var response = await _httpClient.PostAsync($"api/tasks/{id}/complete{q}", null);

            if (!response.IsSuccessStatusCode)
            {
                var reopenResp = await _httpClient.PostAsync($"api/tasks/{id}/reopen{q}", null);
                if (!reopenResp.IsSuccessStatusCode)
                {
                    var error = await reopenResp.Content.ReadAsStringAsync();
                    throw new Exception($"Görev durumu değiştirilemedi: {error}");
                }
            }
        }

        public async Task DeleteAsync(Guid id, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? $"?updatedAt={updatedAt.Value:O}" : "";
            var response = await _httpClient.DeleteAsync($"api/tasks/{id}{q}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Silinemedi: {error}");
            }
        }

        public async Task UpdateTaskStatusAsync(Guid id, StudyTime.Domain.Enums.TaskStatus newStatus, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? $"?updatedAt={updatedAt.Value:O}" : "";
            if (newStatus == StudyTime.Domain.Enums.TaskStatus.Completed)
            {
                var response = await _httpClient.PostAsync($"api/tasks/{id}/complete{q}", null);
                if (!response.IsSuccessStatusCode)
                {
                }
            }
            else
            {
                await _httpClient.PostAsync($"api/tasks/{id}/reopen", null);
            }
        }
    }
}
