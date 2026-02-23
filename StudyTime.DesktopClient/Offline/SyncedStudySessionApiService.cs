using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="StudySessionApiService"/> için Cache-First / Queue-on-Write dekoratörü.
    /// <br/>
    /// <b>Online:</b> Tüm çağrılar uzak servise delege edilir.<br/>
    /// <b>Offline Start:</b> Lokal Guid üretilir, OutboxQueue'ya eklenir — timer çalışmaya devam eder.<br/>
    /// <b>Offline Stop:</b> OutboxQueue'ya başlangıç/bitiş zamanıyla birlikte eklenir.<br/>
    /// <b>Offline Pause/Resume:</b> Sessizce yoksayılır (kritik değil, istatistikleri bozmaz).
    /// </summary>
    public class SyncedStudySessionApiService(
        StudySessionApiService remote,
        OutboxProcessor        outbox,
        ConnectivityService    connectivity)
    {
        // ── START ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ders oturumu başlatır.
        /// Online ise uzak API'ye delege eder.
        /// Offline ise lokal bir Guid üretir ve outbox'a start bilgisini kaydeder.
        /// Her durumda geçerli bir Guid döndürür — timer her zaman başlar.
        /// </summary>
        public async Task<Guid> StartSessionAsync(Guid lessonId, Guid? taskId, bool isBreak = false)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var sessionId = await remote.StartSessionAsync(lessonId, taskId, isBreak);
                    if (sessionId != Guid.Empty)
                        return sessionId;
                }
                catch
                {
                    // API erişilemez → offline path
                }
            }

            // Offline: lokal ID üret, outbox'a kaydet
            var localId = Guid.NewGuid();
            await outbox.EnqueueAsync("StudySession", "Start", new StudySessionStartPayload
            {
                LocalSessionId = localId,
                LessonId       = lessonId,
                TaskId         = taskId,
                IsBreak        = isBreak,
                StartedAt      = DateTime.UtcNow
            });
            return localId;
        }

        // ── STOP ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Oturumu bitirir.
        /// Online → uzak API. Offline → outbox'a ekle.
        /// </summary>
        public async Task StopSessionAsync(Guid sessionId)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    await remote.StopSessionAsync(sessionId);
                    return;
                }
                catch
                {
                    // API erişilemez → outbox'a düş
                }
            }

            // Offline: outbox'a kaydet (sadece gerçek session ID'leri replay'de API'ye gider)
            await outbox.EnqueueAsync("StudySession", "Stop", new StudySessionStopPayload
            {
                LocalSessionId = sessionId,
                StoppedAt      = DateTime.UtcNow
            });
        }

        // ── PAUSE / RESUME ────────────────────────────────────────────────────

        /// <summary>
        /// Oturumu duraklatır. Offline ise yoksayılır.
        /// </summary>
        public async Task PauseSessionAsync(Guid sessionId)
        {
            if (!connectivity.IsOnline) return;
            try { await remote.PauseSessionAsync(sessionId); } catch { /* yoksay */ }
        }

        /// <summary>
        /// Oturumu devam ettirir. Offline ise yoksayılır.
        /// </summary>
        public async Task ResumeSessionAsync(Guid sessionId)
        {
            if (!connectivity.IsOnline) return;
            try { await remote.ResumeSessionAsync(sessionId); } catch { /* yoksay */ }
        }
    }

}
