using Microsoft.Extensions.Logging;
using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
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

            // HTTP
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7288"
                : "https://localhost:7288";

            builder.Services.AddScoped(sp =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl)
                };
            });

            // API SERVICES (Repository implement edenler)
        

            // APPLICATION SERVICES
            builder.Services.AddScoped<DashboardService>();

            // UI SERVICES
            builder.Services.AddSingleton<GlobalTimerService>();
            builder.Services.AddSingleton<ThemeService>();

            return builder.Build();
        }
    }
}
