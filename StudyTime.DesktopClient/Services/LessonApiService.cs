using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using System.Net.Http.Json;

namespace StudyTime.DesktopClient.Services
{
    public class LessonApiService(HttpClient http)
    {
        // Tüm dersleri getir (LessonListItemDto döner)
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            try
            {
                var result = await http.GetFromJsonAsync<List<LessonListItemDto>>("/api/lessons");
                return result ?? new List<LessonListItemDto>();
            }
            catch
            {
                return new List<LessonListItemDto>();
            }
        }

        // 👇 EKLENEN METOT: Workspace Sayfası için Detay Getir
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

        // Yeni ders oluştur
        public async Task<string?> CreateAsync(CreateLessonDto lesson)
        {
            var response = await http.PostAsJsonAsync("/api/lessons", lesson);

            if (response.IsSuccessStatusCode)
            {
                return null; // Başarılı, hata yok
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return !string.IsNullOrWhiteSpace(errorContent) ? errorContent : response.ReasonPhrase;
        }

        // Ders Sil
        public async Task<bool> DeleteAsync(Guid id)
        {
            var response = await http.DeleteAsync($"/api/lessons/{id}");
            return response.IsSuccessStatusCode;
        }
        // 1. Notları Güncelle
        public async Task<bool> UpdateNotesAsync(Guid lessonId, string notes)
        {
            // Backend'de LessonController'da UpdateNotes endpoint'i olduğunu varsayıyoruz
            // Eğer yoksa basitçe bir PUT isteği atacağız
            var response = await http.PutAsJsonAsync($"/api/lessons/{lessonId}/notes", notes);
            return response.IsSuccessStatusCode;
        }

        // 2. Hızlı Task Ekle
        public async Task<bool> CreateTaskAsync(CreateTaskDto taskDto)
        {
            var response = await http.PostAsJsonAsync("/api/tasks", taskDto);
            return response.IsSuccessStatusCode;
        }
    }
}