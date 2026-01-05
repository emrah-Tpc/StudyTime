using Microsoft.Extensions.Logging;
using StudyTime.DesktopClient.Services;

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
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7288"
                : "https://localhost:7288";

            builder.Services.AddScoped(sp =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl)
                };
            });

            // --- API Servisleri ---
            builder.Services.AddScoped<LessonApiService>();
            builder.Services.AddScoped<TaskApiService>();
            builder.Services.AddScoped<StudySessionApiService>();
            builder.Services.AddScoped<DashboardApiService>();

            // --- UI Servisleri ---
            builder.Services.AddSingleton<GlobalTimerService>();

            // 👇 BU EKSİKTİ, MUTLAKA EKLEYİN 👇
            builder.Services.AddSingleton<ThemeService>();

            return builder.Build();
        }
    }
}