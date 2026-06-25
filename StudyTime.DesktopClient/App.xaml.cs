using StudyTime.DesktopClient.Services;
#if WINDOWS
using StudyTime.DesktopClient.Platforms.Windows;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Dispatching;

namespace StudyTime.DesktopClient
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly IServiceProvider _services;
        private int _startupInitState;

        public App(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
        }

        protected override async void OnStart()
        {
            base.OnStart();

            try
            {
                // Preload tokens on the UI thread so they are cached in memory.
                // This prevents SecureStorage deadlocks when accessed from the background thread.
                var authStateProvider = IPlatformApplication.Current?.Services.GetService<StudyTime.DesktopClient.Services.CustomAuthenticationStateProvider>();
                if (authStateProvider != null)
                {
                    await authStateProvider.GetTokenAsync();
                    await authStateProvider.GetRefreshTokenAsync();
                }
            }
            catch { }

            try
            {
                var syncService = IPlatformApplication.Current?.Services.GetService<StudyTime.DesktopClient.Services.SyncBackgroundService>();
                syncService?.Start();
            }
            catch { }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                var window = new Window(new MainPage())
                {
                    Title         = "StudyTime",
                    MinimumWidth  = 900,
                    MinimumHeight = 620
                };

                _ = InitializeOptionalStartupIntegrationsAsync(window);
                return window;
            }
            catch (Exception ex)
            {
                var logger = _services.GetService<ILogger<App>>();
                logger?.LogCritical(ex, "CreateWindow failed.");
                throw;
            }
        }

        private async Task InitializeOptionalStartupIntegrationsAsync(Window window)
        {
            try
            {
                // Başlatma entegrasyonları yalnızca bir kez kurulmalı.
                if (Interlocked.Exchange(ref _startupInitState, 1) == 1)
                {
                    return;
                }

                // Pencere oluşturma path'ini bloklamamak için bir tick sonra UI thread'de başlat.
                await Task.Delay(150).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _ = _services.GetRequiredService<TimerNotificationService>();
                    _ = _services.GetRequiredService<TaskReminderNotificationService>();
                    _services.GetRequiredService<SyncBackgroundService>().Start();

#if WINDOWS
                    var tray = _services.GetRequiredService<TrayIconService>();
                    tray.Initialize();

                    var timer = _services.GetRequiredService<GlobalTimerService>();
                    timer.OnTimerFinished += () => tray.ShowFinishedBalloon("⏱ Çalışma Tamamlandı!", "Mola zamanı 🎉");
                    timer.OnBreakFinished += () => tray.ShowFinishedBalloon("✅ Mola Bitti!", "Yeniden odaklanma zamanı 💪");

                    window.Destroying += (s, e) => tray.Dispose();
#endif
                });
            }
            catch (Exception ex)
            {
                var logger = _services.GetService<ILogger<App>>();
                logger?.LogError(ex, "Optional startup integrations failed.");
            }
        }
    }
}
