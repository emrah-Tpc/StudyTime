using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.DTOs.StudySessions;
using StudyTime.DesktopClient.Services;
using StudyTime.DesktopClient;
using SQLite;

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
        private readonly SyncStatusService     _syncStatus;
        private readonly ILogger<OutboxProcessor> _logger;
        private readonly SemaphoreSlim         _flushLock = new(1, 1);

        /// <summary>
        /// Yerel geçici Id sunucu Id'si ile eşlendiğinde tetiklenir (ana iş parçacığında).
        /// Blazor sayfaları abone olurken <c>IDisposable</c> ile çıkışta aboneliği kaldırmalıdır.
        /// </summary>
        public event EventHandler<LocalIdReconciledEventArgs>? LocalIdReconciled;

        public OutboxProcessor(
            LocalDb              db,
            ConnectivityService  connectivity,
            IServiceScopeFactory scopeFactory,
            SyncStatusService    syncStatus,
            ILogger<OutboxProcessor> logger)
        {
            _db           = db;
            _connectivity = connectivity;
            _scopeFactory = scopeFactory;
            _syncStatus   = syncStatus;
            _logger       = logger;

            if (MauiProgram.IsOfflineBeta)
                return;

            // Bağlantı geldiğinde otomatik flush
            _connectivity.OnChanged += async (isOnline) =>
            {
                if (MauiProgram.IsOfflineBeta) return;
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

            _syncStatus.SetPendingItems(true);
        }

        // ── Flush ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Kuyrukta bekleyen tüm işlemleri API'ye gönderir.
        /// Başarılı olanlar silinir; başarısız olanların RetryCount artar.
        /// Maksimum 3 deneme sonrası kayıt <see cref="DeadLetterEntry"/> tablosuna taşınır ve silinmez (sessiz silme yok).
        /// </summary>
        public async Task FlushAsync()
        {
            if (MauiProgram.IsOfflineBeta) return;

            if (!await _flushLock.WaitAsync(0)) return; // Zaten çalışıyor
            try
            {
                _syncStatus.SetSyncing(true);
                _syncStatus.SetError(null);

                var conn    = await _db.GetAsync();
                // Aynı CreatedAt'te bile Lesson → Task → StudySession → Notification
                var pending = (await conn.Table<OutboxEntry>().ToListAsync())
                    .OrderBy(e => e.CreatedAt)
                    .ThenBy(e => GetEntityOrder(e.EntityType))
                    .ToList();

                foreach (var entry in pending)
                {
                    var (success, error) = await ReplayAsync(entry);

                    if (success)
                    {
                        await conn.DeleteAsync(entry);
                    }
                    else
                    {
                        entry.RetryCount++;
                        if (entry.RetryCount >= 3)
                        {
                            await SaveDeadLetterAsync(conn, entry, error);
                            await conn.DeleteAsync(entry);
                            _logger.LogWarning(
                                "Outbox dead-letter: {Entity}/{Op} retries={Retries} error={Error}",
                                entry.EntityType, entry.Operation, entry.RetryCount, error);
                        }
                        else
                        {
                            await conn.UpdateAsync(entry);
                            _syncStatus.SetError("Some items failed to sync.");
                        }
                    }
                }

                var remaining = await conn.Table<OutboxEntry>().CountAsync();
                _syncStatus.SetPendingItems(remaining > 0);
            }
            catch (Exception ex)
            {
                _syncStatus.SetError(ex.Message);
                _logger.LogError(ex, "Outbox flush failed");
            }
            finally
            {
                _syncStatus.SetSyncing(false);
                _flushLock.Release();
            }
        }

        private static async Task SaveDeadLetterAsync(SQLiteAsyncConnection conn, OutboxEntry entry, string? lastError)
        {
            var dead = new DeadLetterEntry
            {
                EntityType = entry.EntityType,
                Operation  = entry.Operation,
                Payload    = entry.Payload,
                CreatedAt  = entry.CreatedAt,
                RetryCount = entry.RetryCount,
                FailedAt   = DateTime.UtcNow,
                LastError  = lastError
            };
            await conn.InsertAsync(dead);
        }

        // ── Replay ───────────────────────────────────────────────────────────

        private async Task<(bool Success, string? Error)> ReplayAsync(OutboxEntry entry)
        {
            using var scope      = _scopeFactory.CreateScope();
            var lessonApi  = scope.ServiceProvider.GetRequiredService<LessonApiService>();
            var taskApi    = scope.ServiceProvider.GetRequiredService<TaskApiService>();
            var sessionApi = scope.ServiceProvider.GetRequiredService<StudySessionApiService>();
            var noteApi    = scope.ServiceProvider.GetRequiredService<NotificationApiService>();

            try
            {
                var conn = await _db.GetAsync();

                // ── Lesson operasyonları ──────────────────────────────────────
                if (entry.EntityType == "Lesson")
                {
                    var lessonCache = scope.ServiceProvider.GetRequiredService<LocalLessonCache>();
                    switch (entry.Operation)
                    {
                        case "Create":
                        {
                            var wrapped = JsonSerializer.Deserialize<LessonCreateOutboxPayload>(entry.Payload);
                            if (wrapped?.Dto != null && wrapped.ClientTempId != Guid.Empty)
                            {
                                var serverId = await lessonApi.CreateReturningIdAsync(wrapped.Dto);
                                await conn.InsertOrReplaceAsync(new TempIdMapEntry
                                {
                                    EntityType = "Lesson",
                                    TempId     = wrapped.ClientTempId,
                                    ServerId   = serverId
                                });
                                await lessonCache.ReconcileCreateIdAsync(wrapped.ClientTempId, serverId);
                                RaiseLocalIdReconciled("Lesson", wrapped.ClientTempId, serverId);
                                return (true, null);
                            }

                            var createDto = JsonSerializer.Deserialize<CreateLessonDto>(entry.Payload);
                            if (createDto == null)
                                return (false, "Invalid Lesson Create payload.");
                            await lessonApi.CreateReturningIdAsync(createDto);
                            return (true, null);
                        }

                        case "Delete":
                        {
                            var lessonId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            lessonId = await ResolveMappedIdAsync(conn, lessonId);
                            // Bypass Last-Write-Wins conflict check for offline outbox syncs
                            await lessonApi.DeleteAsync(lessonId, null);
                            return (true, null);
                        }

                        case "Archive":
                        {
                            var lessonId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            lessonId = await ResolveMappedIdAsync(conn, lessonId);
                            await lessonApi.ArchiveAsync(lessonId, null);
                            return (true, null);
                        }

                        case "Restore":
                        {
                            var lessonId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            lessonId = await ResolveMappedIdAsync(conn, lessonId);
                            await lessonApi.RestoreAsync(lessonId, null);
                            return (true, null);
                        }

                        case "UpdateNotes":
                        {
                            var notesPayload = JsonSerializer.Deserialize<UpdateNotesPayload>(entry.Payload);
                            if (notesPayload == null)
                                return (false, "Invalid Lesson UpdateNotes payload.");
                            var lessonId = await ResolveMappedIdAsync(conn, notesPayload.LessonId);
                            await lessonApi.UpdateNotesAsync(lessonId, notesPayload.Notes, null);
                            return (true, null);
                        }
                    }
                }

                // ── Task operasyonları ────────────────────────────────────────
                if (entry.EntityType == "Task")
                {
                    var taskCache = scope.ServiceProvider.GetRequiredService<LocalTaskCache>();
                    switch (entry.Operation)
                    {
                        case "Create":
                        {
                            var wrapped = JsonSerializer.Deserialize<TaskCreateOutboxPayload>(entry.Payload);
                            if (wrapped?.Dto != null && wrapped.ClientTempId != Guid.Empty)
                            {
                                await ResolveCreateTaskDtoLessonIdAsync(conn, wrapped.Dto);
                                var serverId = await taskApi.CreateAsync(wrapped.Dto);
                                await conn.InsertOrReplaceAsync(new TempIdMapEntry
                                {
                                    EntityType = "Task",
                                    TempId     = wrapped.ClientTempId,
                                    ServerId   = serverId
                                });
                                await taskCache.ReconcileCreateIdAsync(wrapped.ClientTempId, serverId);
                                RaiseLocalIdReconciled("Task", wrapped.ClientTempId, serverId);
                                return (true, null);
                            }

                            var legacyDto = JsonSerializer.Deserialize<CreateTaskDto>(entry.Payload);
                            if (legacyDto == null)
                                return (false, "Invalid Task Create payload.");
                            await ResolveCreateTaskDtoLessonIdAsync(conn, legacyDto);
                            await taskApi.CreateAsync(legacyDto);
                            return (true, null);
                        }

                        case "Delete":
                        {
                            var taskId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            taskId = await ResolveMappedIdAsync(conn, taskId);
                            // Bypass Last-Write-Wins conflict check for offline outbox syncs
                            await taskApi.DeleteAsync(taskId, null);
                            return (true, null);
                        }

                        case "Toggle":
                        {
                            var toggleId = JsonSerializer.Deserialize<Guid>(entry.Payload);
                            toggleId = await ResolveMappedIdAsync(conn, toggleId);
                            // Bypass Last-Write-Wins conflict check for offline outbox syncs
                            await taskApi.ToggleCompleteAsync(toggleId, null);
                            return (true, null);
                        }

                        case "Update":
                        {
                            var updatePayload = JsonSerializer.Deserialize<TaskUpdatePayload>(entry.Payload);
                            if (updatePayload?.Dto == null)
                                return (false, "Invalid Task Update payload.");
                            var effectiveId = await ResolveMappedIdAsync(conn, updatePayload.Id);
                            await ResolveUpdateTaskDtoLessonIdAsync(conn, updatePayload.Dto);
                            // Bypass Last-Write-Wins conflict check for offline outbox syncs
                            updatePayload.Dto.UpdatedAt = null;
                            await taskApi.UpdateAsync(effectiveId, updatePayload.Dto);
                            return (true, null);
                        }
                    }
                }

                // ── StudySession operasyonları ─────────────────────────────
                if (entry.EntityType == "StudySession")
                {
                    switch (entry.Operation)
                    {
                        case "Start":
                            var startPayload = JsonSerializer.Deserialize<StudySessionStartPayload>(entry.Payload);
                            if (startPayload == null)
                                return (false, "Invalid StudySession Start payload.");
                            var lessonIdResolved = await ResolveMappedIdAsync(conn, startPayload.LessonId);
                            Guid? taskIdResolved = startPayload.TaskId.HasValue
                                ? await ResolveMappedIdAsync(conn, startPayload.TaskId.Value)
                                : null;
                            var serverId = await sessionApi.StartSessionAsync(
                                lessonIdResolved, taskIdResolved, startPayload.IsBreak);
                            if (serverId == Guid.Empty)
                                return (false, "StartSession API returned empty session id.");
                            await conn.InsertOrReplaceAsync(new SessionServerIdMapEntry
                            {
                                LocalSessionId  = startPayload.LocalSessionId,
                                ServerSessionId = serverId,
                                MappedAt        = DateTime.UtcNow
                            });
                            RaiseLocalIdReconciled("StudySession", startPayload.LocalSessionId, serverId);
                            return (true, null);

                        case "Stop":
                            var stopPayload = JsonSerializer.Deserialize<StudySessionStopPayload>(entry.Payload);
                            if (stopPayload == null)
                                return (false, "Invalid StudySession Stop payload.");
                            var map = await conn.Table<SessionServerIdMapEntry>()
                                .Where(m => m.LocalSessionId == stopPayload.LocalSessionId)
                                .FirstOrDefaultAsync();
                            if (map == null)
                                return (false, "No SessionServerIdMap for local session; ensure Start replay ran first.");
                            await sessionApi.StopSessionAsync(map.ServerSessionId, stopPayload.StoppedAt);
                            await conn.DeleteAsync(map);
                            return (true, null);
                    }
                }

                // ── Notification operasyonları ─────────────────────────────
                if (entry.EntityType == "Notification")
                {
                    switch (entry.Operation)
                    {
                        case "Create":
                            var notification = JsonSerializer.Deserialize<StudyTime.Domain.Entities.Notification>(entry.Payload);
                            if (notification == null)
                                return (false, "Invalid Notification Create payload.");
                            var originalId = notification.Id;
                            var serverId = await noteApi.CreateAsync(notification);
                            if (serverId != Guid.Empty && serverId != originalId)
                            {
                                var noteCache = scope.ServiceProvider.GetRequiredService<LocalNotificationCache>();
                                await conn.InsertOrReplaceAsync(new TempIdMapEntry
                                {
                                    EntityType = "Notification",
                                    TempId = originalId,
                                    ServerId = serverId
                                });
                                await noteCache.ReconcileIdAsync(originalId, serverId);
                                RaiseLocalIdReconciled("Notification", originalId, serverId);
                            }
                            return (true, null);
                    }
                }

                return (false, $"Unknown entity/operation: {entry.EntityType}/{entry.Operation}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Outbox, dead-letter ve oturum Id eşlemesini temizler (logout / tam yerel wipe).
        /// </summary>
        public async Task ClearAllQueuedDataAsync()
        {
            var conn = await _db.GetAsync();
            await conn.DeleteAllAsync<OutboxEntry>();
            await conn.DeleteAllAsync<DeadLetterEntry>();
            await conn.DeleteAllAsync<SessionServerIdMapEntry>();
            await conn.DeleteAllAsync<TempIdMapEntry>();
        }

        private static int GetEntityOrder(string entityType)
        {
            return entityType switch
            {
                "Lesson" => 1,
                "Task" => 2,
                "StudySession" => 3,
                "Notification" => 4,
                _ => 5
            };
        }

        private static async Task<Guid> ResolveMappedIdAsync(SQLiteAsyncConnection conn, Guid id)
        {
            var row = await conn.Table<TempIdMapEntry>().Where(m => m.TempId == id).FirstOrDefaultAsync();
            return row?.ServerId ?? id;
        }

        /// <summary>
        /// Çevrimdışı oluşturulan dersin geçici LessonId'si, görev POST/PUT öncesi sunucu FK'sine uygun gerçek Id'ye çevrilir.
        /// </summary>
        private static async Task ResolveCreateTaskDtoLessonIdAsync(SQLiteAsyncConnection conn, CreateTaskDto dto)
        {
            if (!dto.LessonId.HasValue) return;
            dto.LessonId = await ResolveMappedIdAsync(conn, dto.LessonId.Value);
        }

        private static async Task ResolveUpdateTaskDtoLessonIdAsync(SQLiteAsyncConnection conn, UpdateTaskDto dto)
        {
            if (!dto.LessonId.HasValue) return;
            dto.LessonId = await ResolveMappedIdAsync(conn, dto.LessonId.Value);
        }

        /// <summary>
        /// Abonelerin UI güvenliği için olay ana iş parçacığında yayınlanır.
        /// </summary>
        private void RaiseLocalIdReconciled(string entityType, Guid tempId, Guid serverId)
        {
            if (tempId == serverId) return;

            void Fire()
            {
                try
                {
                    LocalIdReconciled?.Invoke(this, new LocalIdReconciledEventArgs
                    {
                        EntityType = entityType,
                        TempId     = tempId,
                        ServerId   = serverId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LocalIdReconciled handler threw for {EntityType}", entityType);
                }
            }

            if (MainThread.IsMainThread)
                Fire();
            else
                MainThread.BeginInvokeOnMainThread(Fire);
        }

        public void Dispose()
        {
            _flushLock.Dispose();
        }

        // ── Internal payload helper ───────────────────────────────────────────
        private record UpdateNotesPayload(Guid LessonId, string Notes);
    }
}
