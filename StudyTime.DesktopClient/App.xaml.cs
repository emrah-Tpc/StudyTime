using StudyTime.DesktopClient.Services;
#if WINDOWS
using StudyTime.DesktopClient.Platforms.Windows;
#endif

namespace StudyTime.DesktopClient
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage())
            {
                Title         = "StudyTime",
                MinimumWidth  = 900,
                MinimumHeight = 620
            };

#if WINDOWS
            // Sistem tepsisi başlat — STA thread'de (CreateWindow UI thread'inde çalışır)
            var tray = _services.GetRequiredService<TrayIconService>();
            tray.Initialize();

            // TrayIcon: timer bitiminde balon bildirimi tetikle
            var timer = _services.GetRequiredService<GlobalTimerService>();
            timer.OnTimerFinished += () => tray.ShowFinishedBalloon("⏱ Çalışma Tamamlandı!", "Mola zamanı 🎉");
            timer.OnBreakFinished += () => tray.ShowFinishedBalloon("✅ Mola Bitti!", "Yeniden odaklanma zamanı 💪");

            // Pencere kapanırken tray icon temizle
            window.Destroying += (s, e) => tray.Dispose();
#endif

            return window;
        }
    }
}
