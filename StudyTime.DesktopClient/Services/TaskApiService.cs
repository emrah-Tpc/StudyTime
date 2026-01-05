using System.Net.Http.Json;
using StudyTime.Application.DTOs.Tasks;

namespace StudyTime.DesktopClient.Services
{
    public class TaskApiService
    {
        private readonly HttpClient _httpClient;

        public TaskApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Görevleri Getir
        public async Task<List<TaskDto>> GetTasksByLessonIdAsync(Guid lessonId)
        {
            try
            {
                var tasks = await _httpClient.GetFromJsonAsync<List<TaskDto>>("api/tasks");
                if (tasks == null) return new List<TaskDto>();

                // 👇 YENİ MANTIK:
                // Eğer gelen ID boşsa (Guid.Empty), GENEL görevleri (LessonId == null) döndür.
                if (lessonId == Guid.Empty)
                {
                    return tasks.Where(t => t.LessonId == null).ToList();
                }

                // Aksi takdirde o derse ait görevleri döndür.
                return tasks.Where(t => t.LessonId == lessonId).ToList();
            }
            catch
            {
                return new List<TaskDto>();
            }
        }

        // Yeni Görev Ekle
        public async Task CreateAsync(CreateTaskDto dto)
        {
            // Adres "api/tasks" olarak sabitlendi
            var response = await _httpClient.PostAsJsonAsync("api/tasks", dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Hatası: {error}");
            }
        }

        // Görevi Tamamla / Geri Al
        public async Task ToggleCompleteAsync(Guid id)
        {
            // Önce Complete deniyoruz
            var response = await _httpClient.PostAsync($"api/tasks/{id}/complete", null);

            // Eğer başarısızsa (zaten tamamlanmışsa) Reopen deniyoruz
            if (!response.IsSuccessStatusCode)
            {
                await _httpClient.PostAsync($"api/tasks/{id}/reopen", null);
            }
        }

        // Görevi Sil
        public async Task DeleteAsync(Guid id)
        {
            // DELETE metodunu kullanıyoruz
            var response = await _httpClient.DeleteAsync($"api/tasks/{id}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Silinemedi: {error}");
            }
        }
    }
}