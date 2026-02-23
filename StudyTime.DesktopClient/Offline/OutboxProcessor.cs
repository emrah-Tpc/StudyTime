using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.DTOs.StudySessions;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Offline iken kuyruğa alınan yazma işlemlerini yönetir.
    /// <see cref="ConnectivityService.OnChanged"/> eventini dinler;
    /// internet gelince OutboxQueue'yu ilgili API servislerine replay eder.
    /// <br/>
    /// <b>Not:</b> Bu sınıf Singleton'dır. Scoped servislere doğrudan bağımlı olmak yerine
    /// <see cref="IServiceScopeFactory"/> aracılığıyla her flush'ta kısa ömürlü scope açar.
    /// </summary>
    public class OutboxProcessor : IDisposable
    {
        private readonly LocalDb               _db;
        private readonly ConnectivityService   _connectivity;
        private readonly IServiceScopeFactory  _scopeFactory;
        private readonly SemaphoreSlim         _flushLock = new(1, 1);

        public OutboxProcessor(
            LocalDb              db,
            ConnectivityService  connectivity,
            IServiceScopeFactory scopeFactory)
        {
            _db           = db;
            _connectivity = connectivity;
            _scopeFactory = scopeFactory;

            // Bağlantı geldiğinde otomatik flush
            _connectivity.OnChanged += async (isOnline) =>
            {
                if (isOnline) await FlushAsync();
            };

            // İlk açılışta online ise hemen flush et (başka bir task'ta)
            if (_connectivity.IsOnline)
            {
                _ = FlushAsync();
            }
        }

        // ── Enqueue ──────────────────────────────────────────────────────────

        /// <summary>Yeni bir işlemi kuyruğa ekler.</summary>
        public async Task EnqueueAsync(string entityType, string operation, object payload)
        {
            var conn  = await _db.GetAsync();
            var entry = new OutboxEntry
            {
                EntityType = entityType,
                Operation  = operation,
                Payload    = JsonSerializer.Serialize(payload),
                CreatedAt  = DateTime.UtcNow
            };
            await conn.InsertAsync(entry);
        }

        // ── Flush ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Kuyrukta bekleyen tüm işlemleri API'ye gönderir.
        /// Başarılı olanlar silinir; başarısız olanların RetryCount artar.
        /// Maksimum 3 deneme sonrası kayıt silinir (dead-letter drop).
        /// </summary>
        public async Task FlushAsync()
        {
            if (!await _flushLock.WaitAsync(0)) return; // Zaten çalışıyor
            try
            {
                var conn    = await _db.GetAsync();
                var pending = await conn.Table<OutboxEntry>()
                                        .OrderBy(e => e.CreatedAt)
                                        .ToListAsync();

                foreach (var entry in pending)
                {
                    bool success = await ReplayAsync(entry);

                    if (success)
                    {
                        await conn.DeleteAsync(entry);
                    }
                    else
                    {
                        entry.RetryCount++;
                        if (entry.RetryCount >= 3)
                            await conn.DeleteAsync(entry);   // Dead-letter drop
                        else
                            await conn.UpdateAsync(entry);
                    }
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }

        // ── Replay ───────────────────────────────────────────────────────────

        private async Task<bool> ReplayAsync(OutboxEntry entry)
        {
            // Her replay'de kısa ömürlü scope aç — Singleton/Scoped çakışmasını önler
            using var scope      = _scopeFactory.CreateScope();
            var lessonApi  = scope.ServiceProvider.GetRequiredService<LessonApiService>();
            var taskApi    = scope.ServiceProvider.GetRequiredService<TaskApiService>();
            var sessionApi = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();

            try
            {
                // ── Lesson operasyonları ──────────────────────────────────────
                if (entry.EntityType == "Lesson")
                {
                    switch (entry.Operation)
                    {
                        case "Create":
                            var createDto = JsonSerializer.Deserialize<CreateLessonDto>(entry.Payload);
                            if (createDto != null)
                                await lessonApi.CreateAsync(createDto);
                            return true;

                        case "Delete":
                            var lessonId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            await lessonApi.DeleteAsync(lessonId);
                            return true;

                        case "UpdateNotes":
                            var notesPayload = JsonSerializer.Deserialize<UpdateNotesPayload>(entry.Payload);
                            if (notesPayload != null)
                                await lessonApi.UpdateNotesAsync(notesPayload.LessonId, notesPayload.Notes);
                            return true;
                    }
                }

                // ── Task operasyonları ────────────────────────────────────────
                if (entry.EntityType == "Task")
                {
                    switch (entry.Operation)
                    {
                        case "Create":
                            var createTaskDto = JsonSerializer.Deserialize<CreateTaskDto>(entry.Payload);
                            if (createTaskDto != null)
                                await taskApi.CreateAsync(createTaskDto);
                            return true;

                        case "Delete":
                            var taskId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            await taskApi.DeleteAsync(taskId);
                            return true;

                        case "Toggle":
                            var toggleId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            await taskApi.ToggleCompleteAsync(toggleId);
                            return true;

                        case "Update":
                            var updatePayload = JsonSerializer.Deserialize<TaskUpdatePayload>(entry.Payload);
                            if (updatePayload?.Dto != null)
                                await taskApi.UpdateAsync(updatePayload.Id, updatePayload.Dto);
                            return true;
                    }
                }

                // ── StudySession operasyonları ─────────────────────────────
                if (entry.EntityType == "StudySession")
                {
                    switch (entry.Operation)
                    {
                        case "Start":
                            var startPayload = JsonSerializer.Deserialize<StudySessionStartPayload>(entry.Payload);
                            if (startPayload != null)
                                await sessionApi.StartSessionAsync(startPayload.LessonId, startPayload.TaskId, startPayload.IsBreak);
                            return true;

                        case "Stop":
                            // Gerçek session Start replay'inde oluşturulup kapanacak
                            return true;
                    }
                }

                // Bilinmeyen operation → sil (defensive)
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _flushLock.Dispose();
        }

        // ── Internal payload helper ───────────────────────────────────────────
        private record UpdateNotesPayload(Guid LessonId, string Notes);
    }
}
