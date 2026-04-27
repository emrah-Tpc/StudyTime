using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo ve DevicePlatform için gerekli
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
        /// <summary>
        /// Arkadaş dağıtımı / API’siz Offline Beta. Yayın + sunucu modunda <c>false</c> yapın.
        /// </summary>
        public static readonly bool IsOfflineBeta = false;

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

            var appOptions = StudyTimeAppOptions.Load();
            builder.Services.AddSingleton(appOptions);

            // Auth State Provider & Dependencies (LocalDataWipeService aşağıda kayıtlı; ctor çözümlemesi Build sonrası)
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<CustomAuthenticationStateProvider>();
            builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());
            builder.Services.AddTransient<AuthorizationMessageHandler>();
            builder.Services.AddScoped<AuthService>();

            // --- HTTP Client Ayarları ---
            string baseUrl = appOptions.ApiBaseUrl;

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

            // "StudyTimeApi" adlı özel HttpClient, AuthorizationMessageHandler kullanır
            builder.Services.AddHttpClient("StudyTimeApi", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => {
                var handler = new HttpClientHandler();
#if DEBUG
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
                return handler;
            })
            .AddHttpMessageHandler<AuthorizationMessageHandler>();

            // Refresh/login/register gibi çağrılar için handler'sız istemci.
            builder.Services.AddHttpClient("StudyTimeApiNoAuth", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => {
                var handler = new HttpClientHandler();
#if DEBUG
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
                return handler;
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
            builder.Services.AddSingleton<IDeviceIdentityService, WindowsDeviceIdentityService>();
#else
            builder.Services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();
#endif
            // Mobil bildirimler (iOS/Android) — Singleton: eventlere sürekli bağlı olmalı
            builder.Services.AddSingleton<TimerNotificationService>();
            builder.Services.AddSingleton<AppNotificationCenterService>();

            // --- Offline Sync Servisleri ---
            builder.Services.AddSingleton<LocalUserContext>();
            builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
            builder.Services.AddSingleton<ConnectivityService>();
            builder.Services.AddSingleton<SyncStatusService>();
            builder.Services.AddSingleton<LocalDb>();
            builder.Services.AddSingleton<LocalLessonCache>();
            builder.Services.AddSingleton<LocalTaskCache>();
            builder.Services.AddSingleton<OutboxProcessor>();
            builder.Services.AddSingleton<LocalSnapshotCache>();
            builder.Services.AddSingleton<LocalStudySessionCache>();
            builder.Services.AddSingleton<LocalNotificationCache>();
            builder.Services.AddSingleton<LocalDataWipeService>();
            builder.Services.AddScoped<SyncedLessonApiService>();
            builder.Services.AddScoped<SyncedTaskApiService>();
            builder.Services.AddScoped<SyncedDashboardApiService>();
            builder.Services.AddScoped<SyncedStatisticsApiService>();
            builder.Services.AddScoped<StudyTime.DesktopClient.Offline.SyncedNotificationApiService>();
            // Singleton: her API çağrısında IServiceScopeFactory ile StudySessionApiService (Scoped) güvenli şekilde çözülür (C4)
            builder.Services.AddSingleton<SyncedStudySessionApiService>();

            return builder.Build();
        }
    }
}
