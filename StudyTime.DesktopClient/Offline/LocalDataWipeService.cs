namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Çıkış veya hesap değişiminde yerel kullanıcı verisini tamamen siler (gizlilik).
    /// </summary>
    public sealed class LocalDataWipeService(
        LocalLessonCache       lessonCache,
        LocalTaskCache         taskCache,
        LocalSnapshotCache     snapshotCache,
        LocalStudySessionCache sessionCache,
        LocalNotificationCache notificationCache,
        OutboxProcessor        outboxProcessor,
        Services.GlobalTimerService timerService,
        Services.SyncStatusService syncStatusService,
        Services.AppNotificationCenterService notificationCenterService)
    {
        public async Task WipeAllUserLocalDataAsync()
        {
            timerService.ForceReset();
            syncStatusService.Reset();
            notificationCenterService.ClearAll();

            await lessonCache.ClearAsync();
            await taskCache.ClearAsync();
            await snapshotCache.ClearAllAsync();
            await sessionCache.ClearAsync();
            await notificationCache.ClearAsync();
            await outboxProcessor.ClearAllQueuedDataAsync();
        }
    }
}
