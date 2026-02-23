using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.DesktopClient.Services;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="LessonApiService"/> için Cache-First / Queue-on-Write dekoratörü.
    /// <br/>
    /// <b>Okuma:</b> Online → API → cache refresh. Offline → SQLite cache.<br/>
    /// <b>Yazma:</b> Online → direkt API. Offline → OutboxQueue'ya ekle.
    /// </summary>
    public class SyncedLessonApiService(
        LessonApiService    remote,
        LocalLessonCache    cache,
        LocalTaskCache      taskCache,
        OutboxProcessor     outbox,
        ConnectivityService connectivity)
    {
        // ── OKUMA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Dersleri getirir.
        /// Online ise API'den alır ve önbelleği günceller.
        /// Offline ise önbellekten döner.
        /// </summary>
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var fresh = await remote.GetAllAsync();
                    // Arka plan: önbelleği güncelle (await etmeden de yapılabilir,
                    // burada güvenilirlik için awaited)
                    await cache.UpsertAllAsync(fresh);
                    return fresh;
                }
                catch
                {
                    // API erişilemez → önbelleğe düş
                    return await cache.GetAllAsync();
                }
            }

            return await cache.GetAllAsync();
        }

        /// <summary>
        /// Workspace detayları — Offline ise cache'ten birleştirir.
        /// </summary>
        public async Task<WorkspaceDetailDto?> GetWorkspaceDetailAsync(Guid id)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var detail = await remote.GetWorkspaceDetailAsync(id);
                    if (detail != null)
                    {
                        // Cache'i güncelle (optimistik: ders ve görev cache'lerini tazele)
                        // LessonListItemDto'ya çevirip lesson cache'e atabiliriz ama asıl önemli olan taskları güncellemek
                        // Çünkü GetWorkspaceDetailAsync tüm taskları getiriyor.
                        // Gelecekte TaskCache.UpdateFromWorkspaceAsync gibi bir metot eklenebilir.
                        return detail;
                    }
                }
                catch
                {
                    // API hatası → cache'e düş
                }
            }

            // --- OFFLINE FALLBACK ---
            var lesson = await cache.GetByIdAsync(id);
            if (lesson == null) return null;

            var tasks = await taskCache.GetByLessonIdAsync(id);

            return new WorkspaceDetailDto
            {
                Id    = lesson.Id,
                Name  = lesson.Name,
                Color = lesson.Color,
                Note  = lesson.Notes,
                Tasks = tasks.Select(t => new TaskListItemDto
                {
                    Id          = t.Id,
                    Title       = t.Title,
                    Note        = t.Note,
                    IsCompleted = t.Status == Domain.Enums.TaskStatus.Completed,
                    Status      = t.Status,
                    LessonId    = t.LessonId,
                    LessonName  = lesson.Name,
                    LessonColor = lesson.Color
                }).ToList()
            };
        }

        // ── YAZMA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Yeni ders oluşturur.
        /// Offline ise outbox'a ekler ve optimistik başarı döndürür.
        /// </summary>
        public async Task<string?> CreateAsync(CreateLessonDto dto)
        {
            if (connectivity.IsOnline)
                return await remote.CreateAsync(dto);

            await outbox.EnqueueAsync("Lesson", "Create", dto);
            return null; // Optimistik başarı
        }

        /// <summary>
        /// Ders siler.
        /// Offline ise hem local önbelleği temizler hem outbox'a koyar.
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            // Önbellek her zaman güncelle (optimistik UI)
            await cache.DeleteAsync(id);

            if (connectivity.IsOnline)
                return await remote.DeleteAsync(id);

            await outbox.EnqueueAsync("Lesson", "Delete", id);
            return true; // Optimistik başarı
        }

        /// <summary>
        /// Ders notlarını günceller.
        /// Offline ise outbox'a kuyruğa alır.
        /// </summary>
        public async Task<bool> UpdateNotesAsync(Guid lessonId, string notes)
        {
            if (connectivity.IsOnline)
                return await remote.UpdateNotesAsync(lessonId, notes);

            await outbox.EnqueueAsync("Lesson", "UpdateNotes",
                new { LessonId = lessonId, Notes = notes });
            return true;
        }

        /// <summary>
        /// Hızlı görev ekle — lesson task'ı.
        /// Offline desteklenmiyor; API erişilemezse false döner.
        /// </summary>
        public async Task<bool> CreateTaskAsync(CreateTaskDto taskDto)
        {
            if (!connectivity.IsOnline) return false;
            return await remote.CreateTaskAsync(taskDto);
        }

        // ── SYNC ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Manuel olarak outbox'ı flush eder.
        /// Uygulama açılışında çağrılabilir.
        /// </summary>
        public Task FlushOutboxAsync() => outbox.FlushAsync();
    }
}
