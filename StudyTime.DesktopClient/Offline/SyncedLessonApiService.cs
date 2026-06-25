using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.DesktopClient.Services;
using StudyTime.Domain.Enums;

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
                    // Tam liste — sunucuda silinen derslerin yerelde hayalet kalmaması için replace
                    await cache.ReplaceAllAsync(fresh);
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
        /// Çevrimdışı: geçici Guid + yerel cache + outbox; senkron sonrası sunucu Id ile reconcilation.
        /// </summary>
        public async Task<string?> CreateAsync(CreateLessonDto dto)
        {
            if (connectivity.IsOnline)
            {
                try
                {
                    var err = await remote.CreateAsync(dto);
                    if (err != null) return err;
                    var fresh = await remote.GetAllAsync();
                    await cache.ReplaceAllAsync(fresh);
                    return null;
                }
                catch
                {
                    // API erişilemez — offline iyimser oluşturma
                }
            }

            var tempId = Guid.NewGuid();
            var lesson = new LessonListItemDto
            {
                Id     = tempId,
                Name   = dto.Name,
                Color  = dto.Color,
                Type   = dto.Type,
                Status = LessonStatus.Active,
                Notes  = null
            };
            await cache.UpsertAllAsync(new[] { lesson });
            await outbox.EnqueueAsync("Lesson", "Create", new LessonCreateOutboxPayload
            {
                ClientTempId = tempId,
                Dto          = dto
            });
            return null;
        }

        /// <summary>
        /// Ders siler.
        /// Offline ise hem local önbelleği temizler hem outbox'a koyar.
        /// </summary>
        public async Task<bool> ArchiveAsync(Guid id)
        {
            if (connectivity.IsOnline)
            {
                var result = await remote.ArchiveAsync(id);
                if (result)
                {
                    var lesson = await cache.GetByIdAsync(id);
                    if (lesson != null) {
                        lesson.Status = LessonStatus.Archived;
                        await cache.UpsertAllAsync(new[] { lesson });
                    }
                    return true;
                }
                // If API failed (e.g., 401, 500), fallback to offline behavior
            }
            
            // Offline
            var l = await cache.GetByIdAsync(id);
            if (l != null) {
                l.Status = LessonStatus.Archived;
                await cache.UpsertAllAsync(new[] { l });
            }
            await outbox.EnqueueAsync("Lesson", "Archive", id);
            return true;
        }

        public async Task<bool> RestoreAsync(Guid id)
        {
            if (connectivity.IsOnline)
            {
                var result = await remote.RestoreAsync(id);
                if (result)
                {
                    var lesson = await cache.GetByIdAsync(id);
                    if (lesson != null) {
                        lesson.Status = LessonStatus.Active;
                        await cache.UpsertAllAsync(new[] { lesson });
                    }
                    return true;
                }
                // If API failed (e.g., 401, 500), fallback to offline behavior
            }

            // Offline
            var l = await cache.GetByIdAsync(id);
            if (l != null) {
                l.Status = LessonStatus.Active;
                await cache.UpsertAllAsync(new[] { l });
            }
            await outbox.EnqueueAsync("Lesson", "Restore", id);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            // Önbellek her zaman güncelle (optimistik UI)
            await cache.DeleteAsync(id);

            if (connectivity.IsOnline)
            {
                var success = await remote.DeleteAsync(id);
                if (success) return true;
                // If API failed, enqueue to outbox so it doesn't get lost
            }

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
            {
                var success = await remote.UpdateNotesAsync(lessonId, notes);
                if (success) return true;
                // If API failed, enqueue to outbox
            }

            await cache.UpdateNotesLocalAsync(lessonId, notes);
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
