using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Services
{
    /// <summary>
    /// Mobil platformlarda (iOS/Android) timer bitiminde yerel push bildirim gönderir.
    /// Plugin.LocalNotification paketi gerekmez — MAUI Local Notifications kullanılır.
    /// Windows'ta TrayIconService.ShowFinishedBalloon() kullanıldığından bu servis devre dışıdır.
    /// </summary>
    public class TimerNotificationService : IDisposable
    {
        private readonly GlobalTimerService _timer;
        private readonly AppNotificationCenterService _notificationCenter;

        public TimerNotificationService(GlobalTimerService timer, AppNotificationCenterService notificationCenter)
        {
            _timer = timer;
            _notificationCenter = notificationCenter;

            _timer.OnTimerFinished += OnWorkFinished;
            _timer.OnBreakFinished += OnBreakFinished;
        }

        private void OnWorkFinished()
        {
            _notificationCenter.AddNotification(
                "⏱ Çalışma Tamamlandı!",
                "Harika iş! Artık mola zamanı 🎉",
                NotificationCategory.Discipline);
        }

        private void OnBreakFinished()
        {
            _notificationCenter.AddNotification(
                "✅ Mola Bitti!",
                "Yeniden odaklanma zamanı 💪",
                NotificationCategory.Discipline);
        }

        public void Dispose()
        {
            _timer.OnTimerFinished -= OnWorkFinished;
            _timer.OnBreakFinished -= OnBreakFinished;
        }
    }
}
