using StudyTime.Application.DTOs.Tasks;
using StudyTime.DesktopClient.Services;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="TaskApiService"/> için Cache-First / Queue-on-Write dekoratörü.
    /// <br/>
    /// <b>Okuma:</b> Online → API → cache refresh. Offline → SQLite cache.<br/>
    /// <b>Yazma:</b> Online → direkt API. Offline → OutboxQueue'ya ekle.
    /// </summary>
    public class SyncedTaskApiService(
        TaskApiService      remote,
        LocalTaskCache      cache,
        OutboxProcessor     outbox,
        ConnectivityService connectivity)
    {
        // ── OKUMA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Derse ait görevleri getirir.
        /// Online ise API'den alır ve cache'i günceller.
        /// Offline ise cache'ten döner.
        /// </summary>
        public async Task<List<TaskDto>> GetTasksByLessonIdAsync(Guid lessonId)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    // Tüm task'ları API'den çek, cache'e yaz
                    var all = await remote.GetTasksByLessonIdAsync(lessonId);
                    await cache.ReplaceForLessonAsync(lessonId, all);
                    return all;
                }
                catch
                {
                    // API erişilemez → cache'e düş
                    return await cache.GetByLessonIdAsync(lessonId);
                }
            }

            return await cache.GetByLessonIdAsync(lessonId);
        }

        /// <summary>
        /// Tarih aralığındaki görevleri getirir.
        /// Online ise API'den alır ve cache'i günceller.
        /// Offline ise cache'ten döner (EndDate bazlı filtre).
        /// </summary>
        public async Task<List<TaskDto>> GetTasksByDateRangeAsync(DateTime start, DateTime end)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetTasksByDateRangeAsync(start, end);
                    // Date-range endpoint kismi sonuc doner; tum cache'i replace etmek veri kaybi algisi uretir.
                    await cache.UpsertAllAsync(fresh);
                    return fresh;
                }
                catch
                {
                    return await cache.GetByDateRangeAsync(start, end);
                }
            }

            return await cache.GetByDateRangeAsync(start, end);
        }

        // ── YAZMA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Yeni görev oluşturur.
        /// Çevrimdışı: geçici Guid + yerel cache + outbox; senkron sonrası sunucu Id ile reconcilation.
        /// </summary>
        public async Task CreateAsync(CreateTaskDto dto)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    await remote.CreateAsync(dto);
                    if (dto.LessonId.HasValue)
                    {
                        var list = await remote.GetTasksByLessonIdAsync(dto.LessonId.Value);
                        await cache.ReplaceForLessonAsync(dto.LessonId.Value, list);
                    }
                    return;
                }
                catch
                {
                    // API düşerse offline iyimser yola geç
                }
            }

            var tempId = Guid.NewGuid();
            var taskDto = new TaskDto
            {
                Id              = tempId,
                LessonId        = dto.LessonId,
                Title           = dto.Title,
                Note            = dto.Note,
                Status          = dto.Status,
                StartDate       = dto.StartDate,
                EndDate         = dto.EndDate,
                PlannedDuration = dto.PlannedDurationMinutes.HasValue
                    ? TimeSpan.FromMinutes(dto.PlannedDurationMinutes.Value)
                    : null
            };
            await cache.UpsertAllAsync(new[] { taskDto });
            await outbox.EnqueueAsync("Task", "Create", new TaskCreateOutboxPayload
            {
                ClientTempId = tempId,
                Dto          = dto
            });
        }

        /// <summary>
        /// Görevi siler.
        /// Offline ise hem lokal cache'i temizler hem outbox'a koyar.
        /// </summary>
        public async Task DeleteAsync(Guid id)
        {
            // Önce lokal cache'den kaldır (optimistik UI)
            await cache.DeleteAsync(id);

            if (connectivity.IsOnline)
            {
                await remote.DeleteAsync(id);
                return;
            }

            await outbox.EnqueueAsync("Task", "Delete", id);
        }

        /// <summary>
        /// Görevi tamamlandı / geri al olarak işaretler.
        /// Offline ise lokal durumu değiştirir ve outbox'a ekler.
        /// </summary>
        public async Task ToggleCompleteAsync(Guid id)
        {
            // Lokal durum değişimi (optimistik UI)
            await cache.ToggleCompleteAsync(id);

            if (connectivity.IsOnline)
            {
                await remote.ToggleCompleteAsync(id);
                return;
            }

            await outbox.EnqueueAsync("Task", "Toggle", id);
        }

        /// <summary>
        /// Görevi günceller (başlık, tarihler, not vb.).
        /// Offline ise lokal cache'i optimistik günceller ve outbox'a ekler.
        /// </summary>
        public async Task UpdateAsync(Guid id, UpdateTaskDto dto)
        {
            // Lokal cache: buluşan kaydı güncelle (optimistik UI)
            await cache.UpdateAsync(id, dto);

            if (connectivity.IsOnline)
            {
                await remote.UpdateAsync(id, dto);
                return;
            }

            await outbox.EnqueueAsync("Task", "Update", new TaskUpdatePayload { Id = id, Dto = dto });
        }

        /// <summary>
        /// Dashboard için görev durumu güncelleme.
        /// Gerçek zamanlı dashboard operasyonu — yalnızca online.
        /// Offline ise no-op (sakince yok sayılır).
        /// </summary>
        public async Task UpdateTaskStatusAsync(Guid id, StudyTime.Domain.Enums.TaskStatus newStatus)
        {
            // Lokal cache: buluşan kaydı güncelle (optimistik UI)
            await cache.UpdateStatusAsync(id, newStatus);

            if (connectivity.IsOnline)
            {
                await remote.UpdateTaskStatusAsync(id, newStatus);
                return;
            }

            // Fallback for offline if necessary (we use Toggle in outbox since UpdateTaskStatusAsync doesn't have a specific Outbox Payload in this system, 
            // but the original didn't queue anything so we should at least enqueue the Toggle if changing state)
            if (newStatus == StudyTime.Domain.Enums.TaskStatus.Completed || newStatus == StudyTime.Domain.Enums.TaskStatus.Pending)
            {
                await outbox.EnqueueAsync("Task", "Toggle", id);
            }
        }
    }
}
