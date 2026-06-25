using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7288/") };
        var request = new { FullName = "Test", Email = "test2@test.com", Password = "Password123", ClientType = "Desktop" };
        var response = await client.PostAsJsonAsync("api/auth/register", request);
        Console.WriteLine("Status: " + response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine("Content: " + content);
    }
}
