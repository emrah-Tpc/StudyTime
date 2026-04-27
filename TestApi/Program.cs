using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        try {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7196/") };
            
            var dto = new {
                Title = "Test C#",
                LessonId = (Guid?)null,
                StartDate = (DateTime?)null,
                EndDate = (DateTime?)null,
                Note = (string?)null,
                Status = 0,
                PlannedDurationMinutes = (int?)null
            };

            var response = await client.PostAsJsonAsync("api/tasks", dto);
            Console.WriteLine($"StatusCode: {response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"ResponseBody: {body}");
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
