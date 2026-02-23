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
                    await cache.UpsertAllAsync(all);
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
            Console.WriteLine($"[SyncedTask] GetTasksByDateRangeAsync çağrıldı. IsOnline={connectivity.IsOnline}");
            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetTasksByDateRangeAsync(start, end);
                    Console.WriteLine($"[SyncedTask] API'den {fresh.Count} görev alındı, cache'e yazılıyor...");
                    await cache.UpsertAllAsync(fresh);
                    return fresh;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SyncedTask] API hatası: {ex.Message} — cache'e düşülüyor");
                    return await cache.GetByDateRangeAsync(start, end);
                }
            }

            Console.WriteLine($"[SyncedTask] Offline — cache'ten okunuyor");
            return await cache.GetByDateRangeAsync(start, end);
        }

        // ── YAZMA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Yeni görev oluşturur.
        /// Offline ise outbox'a ekler.
        /// </summary>
        public async Task CreateAsync(CreateTaskDto dto)
        {
            if (connectivity.IsOnline)
            {
                await remote.CreateAsync(dto);
                return;
            }

            await outbox.EnqueueAsync("Task", "Create", dto);
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
            if (!connectivity.IsOnline) return;
            await remote.UpdateTaskStatusAsync(id, newStatus);
        }
    }
}
