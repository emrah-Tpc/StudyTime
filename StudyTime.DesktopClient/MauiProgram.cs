using Microsoft.Extensions.Logging;
using StudyTime.Application.Interfaces;
using StudyTime.Application.Services; // Servislerin olduğu namespace
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

            // --- 1. HTTP Client Ayarları ---
            // Android ve Windows için doğru adres seçimi
            // --- 1. HTTP Client Ayarları ---
            // Windows için tekrar localhost yapalım, bazen sertifika yüzünden IP adresi sorun çıkarabilir.
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7288"
                : "https://localhost:7288";

            builder.Services.AddScoped(sp =>
            {
                var handler = new HttpClientHandler
                {
                    // Sertifika hatasını yoksay (Development ortamı için şart)
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl),
                    // Zaman aşımı ekleyelim ki sonsuza kadar dönmesin (10 saniye)
                    Timeout = TimeSpan.FromSeconds(10)
                };
            });

            // --- 2. EKSİK OLAN SERVİSLERİ EKLEDİM ---
            // Bu servisler MauiProgram'da kayıtlı olmadığı için uygulama açılmıyordu.
            builder.Services.AddSingleton<DashboardApiService>();

            builder.Services.AddScoped<DashboardApiService>();
            builder.Services.AddScoped<LessonApiService>();       // <--- EKLENDİ
            builder.Services.AddScoped<TaskApiService>();         // <--- EKLENDİ
            builder.Services.AddScoped<StudySessionApiService>(); // <--- EKLENDİ

            // --- 3. UI Servisleri ---
            builder.Services.AddSingleton<GlobalTimerService>();
            builder.Services.AddSingleton<ThemeService>();

            return builder.Build();
        }
    }
}