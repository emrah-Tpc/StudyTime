using System.Net.Http.Json;
using StudyTime.Application.DTOs.StudySessions;

namespace StudyTime.DesktopClient.Services
{
    public class StudySessionApiService
    {
        private readonly HttpClient _http;

        public StudySessionApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<Guid> StartSessionAsync(Guid lessonId, Guid? taskId)
        {
            // DTO nesnesini oluşturuyoruz (Hatasız)
            var dto = new StartStudySessionDto
            {
                LessonId = lessonId,
                TaskId = taskId
            };

            // API'ye POST isteği atıyoruz
            var response = await _http.PostAsJsonAsync("api/studysession/start", dto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartSessionResponse>();
                return result?.SessionId ?? Guid.Empty;
            }
            return Guid.Empty;
        }

        public async Task StopSessionAsync(Guid sessionId)
        {
            await _http.PostAsync($"api/studysession/{sessionId}/stop", null);
        }
    }

    // API'den dönen cevabı okumak için yardımcı sınıf
    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
    }
}