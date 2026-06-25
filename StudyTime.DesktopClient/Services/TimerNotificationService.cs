using StudyTime.DesktopClient.Interfaces;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Services
{
    /// <summary>
    /// Mobil platformlarda veya Windows'ta timer bitiminde yerel bildirim fırlatır.
    /// Context-aware şekilde UI Toast veya OS (Tray/Push) bildirimini yönetir.
    /// </summary>
    public class TimerNotificationService : IDisposable
    {
        private readonly GlobalTimerService _timer;
        private readonly IAppNotificationService _notificationService;

        public TimerNotificationService(GlobalTimerService timer, IAppNotificationService notificationService)
        {
            _timer = timer;
            _notificationService = notificationService;

            _timer.OnTimerFinished += OnWorkFinished;
            _timer.OnBreakFinished += OnBreakFinished;
            _timer.OnTimerStopped += OnTimerStopped;
        }

        private async void OnWorkFinished()
        {
            await _notificationService.SendNotificationAsync(
                NotificationCategory.Discipline,
                "⏱ Çalışma Tamamlandı!",
                "Harika iş! Artık mola zamanı 🎉");
        }

        private async void OnBreakFinished()
        {
            await _notificationService.SendNotificationAsync(
                NotificationCategory.Discipline,
                "✅ Mola Bitti!",
                "Yeniden odaklanma zamanı 💪");
        }

        private async void OnTimerStopped()
        {
            await _notificationService.SendNotificationAsync(
                NotificationCategory.System,
                "Kronometre Durduruldu",
                "Çalışma oturumu manuel olarak durduruldu.");
        }

        public void Dispose()
        {
            _timer.OnTimerFinished -= OnWorkFinished;
            _timer.OnBreakFinished -= OnBreakFinished;
            _timer.OnTimerStopped -= OnTimerStopped;
        }
    }
}
