using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo ve DevicePlatform için gerekli
using Microsoft.Maui.Storage;
using StudyTime.Application.Interfaces;
using StudyTime.DesktopClient.Services; // Senin servislerin
#if WINDOWS
using StudyTime.DesktopClient.Platforms.Windows;
#endif
using StudyTime.DesktopClient.Offline;
using Microsoft.Maui.Networking;

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
            string baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "https://10.0.2.2:7288"
                : "https://localhost:7288";

            builder.Services.AddScoped(sp =>
            {
                var handler = new HttpClientHandler();

                // Geliştirme ortamında sertifika hatasını yoksay (SSL Bypass)
#if DEBUG
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl),
                    // Offline/API-kapalı durumunda hızlı fallback için kısa timeout
                    Timeout = TimeSpan.FromSeconds(8)
                };
            });

            // --- UI Servisleri ---
            builder.Services.AddScoped<DashboardApiService>();
            builder.Services.AddScoped<LessonApiService>();
            builder.Services.AddScoped<TaskApiService>();
            builder.Services.AddScoped<NotificationApiService>();

            builder.Services.AddSingleton<GlobalTimerService>();
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddScoped<StudySessionApiService>();
            builder.Services.AddScoped<StatisticsApiService>();

            // --- Platform Servisleri ---
#if WINDOWS
            // Sistem tepsisi (Windows) — Singleton: uygulama boyunca tek tray icon
            builder.Services.AddSingleton<TrayIconService>();
#endif
            // Mobil bildirimler (iOS/Android) — Singleton: eventlere sürekli bağlı olmalı
            builder.Services.AddSingleton<TimerNotificationService>();
            builder.Services.AddSingleton<AppNotificationCenterService>();

            // --- Offline Sync Servisleri ---
            builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
            builder.Services.AddSingleton<ConnectivityService>();
            builder.Services.AddSingleton<LocalDb>();
            builder.Services.AddSingleton<LocalLessonCache>();
            builder.Services.AddSingleton<LocalTaskCache>();
            builder.Services.AddSingleton<OutboxProcessor>();
            builder.Services.AddSingleton<LocalSnapshotCache>();
            builder.Services.AddSingleton<LocalStudySessionCache>();
            builder.Services.AddSingleton<LocalNotificationCache>();
            builder.Services.AddScoped<SyncedLessonApiService>();
            builder.Services.AddScoped<SyncedTaskApiService>();
            builder.Services.AddScoped<SyncedDashboardApiService>();
            builder.Services.AddScoped<SyncedStatisticsApiService>();
            builder.Services.AddScoped<StudyTime.DesktopClient.Offline.SyncedNotificationApiService>();
            builder.Services.AddScoped<SyncedStudySessionApiService>(sp =>
            {
                // GlobalTimerService Singleton — SyncedStudySessionApiService de Singleton-safe olmalı
                // HttpClient Scoped olduğundan sp.CreateScope() ile kısa ömürlü bir scope aç
                var scope      = sp.CreateScope();
                var remote     = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
                var outbox     = sp.GetRequiredService<OutboxProcessor>();
                var conn       = sp.GetRequiredService<ConnectivityService>();
                return new SyncedStudySessionApiService(remote, outbox, conn);
            });

            return builder.Build();
        }
    }
}
