using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo ve DevicePlatform için gerekli
using Microsoft.Maui.Storage;
using StudyTime.Application.Interfaces;
using StudyTime.DesktopClient.Services; // Senin servislerin

namespace StudyTime.DesktopClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // --- HTTP Client Ayarları ---
            // Android emülatör için 10.0.2.2, Windows için localhost
            // Not: DeviceInfo kullanabilmek için yukarıya 'using Microsoft.Maui.Devices;' ekledik.
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7288"
                : "https://localhost:7288";

            builder.Services.AddScoped(sp =>
            {
                var handler = new HttpClientHandler
                {
                    // Geliştirme ortamında sertifika hatasını yoksay (SSL Bypass)
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl)
                };
            });

            // --- UI Servisleri ---
            builder.Services.AddScoped<DashboardApiService>();
            builder.Services.AddScoped<LessonApiService>();

            // Eğer GlobalTimerService ve ThemeService sınıfların hazırsa bunları aç:
            builder.Services.AddSingleton<GlobalTimerService>();
            builder.Services.AddSingleton<ThemeService>();

            return builder.Build();
        }
    }
}