using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudyTime.DesktopClient.Services
{
    public class LessonApiService
    {
        private readonly HttpClient http;

        public LessonApiService(IHttpClientFactory factory)
        {
            this.http = factory.CreateClient("StudyTimeApi");
        }

        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            var result = await http.GetFromJsonAsync<List<LessonListItemDto>>("/api/lessons");
            return result ?? new List<LessonListItemDto>();
        }

        public async Task<WorkspaceDetailDto?> GetWorkspaceDetailAsync(Guid id)
        {
            try
            {
                return await http.GetFromJsonAsync<WorkspaceDetailDto>($"/api/lessons/{id}/workspace");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API Hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>Hata mesajı veya null (başarı).</summary>
        public async Task<string?> CreateAsync(CreateLessonDto lesson)
        {
            try
            {
                await CreateReturningIdAsync(lesson);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Sunucunun oluşturduğu ders Id'si (outbox reconcilation).</summary>
        public async Task<Guid> CreateReturningIdAsync(CreateLessonDto lesson)
        {
            var response = await http.PostAsJsonAsync("/api/lessons", lesson);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception(string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Hata" : body);

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("lessonId", out var idEl))
                throw new Exception("Yanıtta lessonId yok.");
            return idEl.GetGuid();
        }

        public async Task<bool> DeleteAsync(Guid id, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? $"?updatedAt={updatedAt.Value:O}" : "";
            var response = await http.DeleteAsync($"/api/lessons/{id}{q}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateNotesAsync(Guid lessonId, string notes, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? $"?updatedAt={updatedAt.Value:O}" : "";
            var response = await http.PutAsJsonAsync($"/api/lessons/{lessonId}/notes{q}", notes);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ArchiveAsync(Guid id, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? "?updatedAt=" + updatedAt.Value.ToString("O") : "";
            var response = await http.PutAsync($"/api/lessons/{id}/archive{q}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RestoreAsync(Guid id, DateTime? updatedAt = null)
        {
            string q = updatedAt.HasValue ? "?updatedAt=" + updatedAt.Value.ToString("O") : "";
            var response = await http.PutAsync($"/api/lessons/{id}/restore{q}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateTaskAsync(CreateTaskDto taskDto)
        {
            var response = await http.PostAsJsonAsync("/api/tasks", taskDto);
            return response.IsSuccessStatusCode;
        }
    }
}
