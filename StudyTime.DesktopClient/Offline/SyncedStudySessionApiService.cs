using Microsoft.Extensions.DependencyInjection;
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
    /// <br/>
    /// Outbox replay tamamlandıktan sonra yerel oturum Id'si sunucu Id'si ile <see cref="SessionServerIdMapEntry"/> üzerinden eşlenir;
    /// çevrimiçi API çağrılarında önce bu eşleme çözülür (C1/C2).
    /// </summary>
    public class SyncedStudySessionApiService(
        IServiceScopeFactory scopeFactory,
        LocalDb              localDb,
        OutboxProcessor      outbox,
        ConnectivityService  connectivity,
        LocalSnapshotCache   snapshotCache)
    {
        private async Task<Guid> ResolveToServerSessionIdAsync(Guid sessionId)
        {
            var conn = await localDb.GetAsync();
            var row  = await conn.FindAsync<SessionServerIdMapEntry>(sessionId);
            return row?.ServerSessionId ?? sessionId;
        }

        private async Task RemoveLocalMappingAsync(Guid localSessionId)
        {
            var conn = await localDb.GetAsync();
            var row  = await conn.FindAsync<SessionServerIdMapEntry>(localSessionId);
            if (row != null)
                await conn.DeleteAsync(row);
        }

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
                    using var scope     = scopeFactory.CreateScope();
                    var remote = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
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
            var resolved = await ResolveToServerSessionIdAsync(sessionId);

            if (connectivity.IsOnline)
            {
                try
                {
                    using var scope  = scopeFactory.CreateScope();
                    var remote = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
                    await remote.StopSessionAsync(resolved);
                    await RemoveLocalMappingAsync(sessionId);
                    await snapshotCache.InvalidateDashboardAndStatisticsAsync();
                    return;
                }
                catch
                {
                    // API erişilemez → outbox'a düş
                }
            }

            await outbox.EnqueueAsync("StudySession", "Stop", new StudySessionStopPayload
            {
                LocalSessionId = sessionId,
                StoppedAt      = DateTime.UtcNow
            });

            await snapshotCache.InvalidateDashboardAndStatisticsAsync();
        }

        // ── PAUSE / RESUME ────────────────────────────────────────────────────

        /// <summary>
        /// Oturumu duraklatır. Offline ise yoksayılır.
        /// </summary>
        public async Task PauseSessionAsync(Guid sessionId)
        {
            if (!connectivity.IsOnline) return;
            try
            {
                var resolved = await ResolveToServerSessionIdAsync(sessionId);
                using var scope  = scopeFactory.CreateScope();
                var remote = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
                await remote.PauseSessionAsync(resolved);
            }
            catch { /* yoksay */ }
        }

        /// <summary>
        /// Oturumu devam ettirir. Offline ise yoksayılır.
        /// </summary>
        public async Task ResumeSessionAsync(Guid sessionId)
        {
            if (!connectivity.IsOnline) return;
            try
            {
                var resolved = await ResolveToServerSessionIdAsync(sessionId);
                using var scope  = scopeFactory.CreateScope();
                var remote = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
                await remote.ResumeSessionAsync(resolved);
            }
            catch { /* yoksay */ }
        }
    }
}
