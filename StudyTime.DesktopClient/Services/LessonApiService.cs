using System.Net.Http.Json;
using StudyTime.Application.DTOs.Lessons;

namespace StudyTime.DesktopClient.Services
{
    public class LessonApiService
    {
        private readonly HttpClient _httpClient;

        public LessonApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Listeleme
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<LessonListItemDto>>("api/lesson") ?? new();
        }

        // Oluşturma
        public async Task CreateAsync(CreateLessonDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/lesson", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Kayıt başarısız: {error}");
            }
        }

        // 👇 EKSİK OLAN METOTLAR BURADA:

        // Arşivle
        public async Task ArchiveAsync(Guid id)
        {
            var response = await _httpClient.PutAsync($"api/lesson/{id}/archive", null);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Arşivleme işlemi başarısız oldu.");
        }

        // Geri Yükle
        public async Task RestoreAsync(Guid id)
        {
            var response = await _httpClient.PutAsync($"api/lesson/{id}/restore", null);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Geri yükleme işlemi başarısız oldu.");
        }

        // Sil
        public async Task DeleteAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"api/lesson/{id}");
            if (!response.IsSuccessStatusCode)
                throw new Exception("Silme işlemi başarısız oldu.");
        }
    }
}