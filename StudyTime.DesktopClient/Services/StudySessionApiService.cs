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
            var dto = new StartStudySessionDto
            {
                LessonId = lessonId,
                TaskId = taskId
            };

            // URL'nin başına / eklendi
            var response = await _http.PostAsJsonAsync("/api/studysession/start", dto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartSessionResponse>();
                return result?.SessionId ?? Guid.Empty;
            }
            return Guid.Empty;
        }

        public async Task PauseSessionAsync(Guid sessionId)
        {
            // URL başına / eklendi ve hata fırlatması için kontrol konuldu
            var response = await _http.PostAsync($"/api/studysession/{sessionId}/pause", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task ResumeSessionAsync(Guid sessionId)
        {
            // URL başına / eklendi
            var response = await _http.PostAsync($"/api/studysession/{sessionId}/resume", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task StopSessionAsync(Guid sessionId)
        {
            // URL başına / eklendi
            var response = await _http.PostAsync($"/api/studysession/{sessionId}/stop", null);
            response.EnsureSuccessStatusCode();
        }
    }

    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
    }
}