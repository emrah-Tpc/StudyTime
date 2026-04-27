using StudyTime.Application.DTOs.Tasks;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="TaskCacheEntry"/> tablosunu yöneten yerel SQLite önbelleği.
    /// Sorgular <see cref="LocalUserContext.UserId"/> ve <see cref="TaskCacheEntry.IsDeleted"/> ile izole edilir.
    /// </summary>
    public class LocalTaskCache(LocalDb db, LocalUserContext userContext)
    {
        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>Geçerli kullanıcıya ait silinmemiş görevler.</summary>
        public async Task<List<TaskDto>> GetAllAsync()
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<TaskDto>();

            var conn    = await db.GetAsync();
            var entries = await conn.Table<TaskCacheEntry>()
                .Where(e => e.UserId == uid && !e.IsDeleted)
                .ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>Belirli derse ait görevler (kullanıcı izolasyonu).</summary>
        public async Task<List<TaskDto>> GetByLessonIdAsync(Guid lessonId)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<TaskDto>();

            var conn    = await db.GetAsync();
            var entries = await conn.Table<TaskCacheEntry>()
                .Where(e => e.LessonId == lessonId && e.UserId == uid && !e.IsDeleted)
                .ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>Tarih aralığı (bellek içi OR filtresi + kullanıcı izolasyonu).</summary>
        public async Task<List<TaskDto>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<TaskDto>();

            long startTicks = start.Ticks;
            long endTicks   = end.Ticks;

            var conn    = await db.GetAsync();
            var entries = await conn.Table<TaskCacheEntry>()
                .Where(e => e.UserId == uid && !e.IsDeleted)
                .ToListAsync();

            return entries
                .Where(e =>
                    (e.StartDateTicks.HasValue &&
                     e.StartDateTicks.Value >= startTicks &&
                     e.StartDateTicks.Value <= endTicks)
                    ||
                    (e.EndDateTicks.HasValue &&
                     e.EndDateTicks.Value >= startTicks &&
                     e.EndDateTicks.Value <= endTicks))
                .Select(ToDto)
                .ToList();
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        public async Task UpsertAllAsync(IEnumerable<TaskDto> dtos)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn    = await db.GetAsync();
            var entries = dtos.Select(d => ToEntry(d, uid)).ToList();

            try
            {
                await conn.RunInTransactionAsync(tran =>
                {
                    foreach (var entry in entries)
                        tran.InsertOrReplace(entry);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TaskCache] UpsertAllAsync HATA: {ex.Message}");
                foreach (var entry in entries)
                {
                    try { await conn.InsertOrReplaceAsync(entry); }
                    catch (Exception ex2) { Console.WriteLine($"[TaskCache] InsertOrReplace HATA ({entry.Id}): {ex2.Message}"); }
                }
            }
        }

        /// <summary>Bu kullanıcının tüm görev satırlarını silip API listesini yazar (tarih aralığı senkronu).</summary>
        public async Task ReplaceAllAsync(IEnumerable<TaskDto> dtos)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            await conn.ExecuteAsync("DELETE FROM TaskCache WHERE UserId = ?", uid);
            await UpsertAllAsync(dtos);
        }

        /// <summary>Bir derse ait görevleri değiştirir — sunucuda silinen görevlerin hayalet kalmaması için.</summary>
        public async Task ReplaceForLessonAsync(Guid lessonId, IEnumerable<TaskDto> dtos)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            await conn.ExecuteAsync("DELETE FROM TaskCache WHERE UserId = ? AND LessonId = ?", uid, lessonId);
            await UpsertAllAsync(dtos);
        }

        /// <summary>Önbellekten tek kayıt siler (fiziksel).</summary>
        public async Task DeleteAsync(Guid id)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            await conn.ExecuteAsync("DELETE FROM TaskCache WHERE Id = ? AND UserId = ?", id, uid);
        }

        public async Task ToggleCompleteAsync(Guid id)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                .Where(e => e.Id == id && e.UserId == uid && !e.IsDeleted)
                .FirstOrDefaultAsync();
            if (entry is null) return;

            entry.Status = entry.Status == TaskStatus.Completed.ToString()
                ? TaskStatus.Pending.ToString()
                : TaskStatus.Completed.ToString();

            await conn.UpdateAsync(entry);
        }

        public async Task UpdateStatusAsync(Guid id, StudyTime.Domain.Enums.TaskStatus newStatus)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                .Where(e => e.Id == id && e.UserId == uid && !e.IsDeleted)
                .FirstOrDefaultAsync();
            if (entry is null) return;

            entry.Status = newStatus.ToString();
            await conn.UpdateAsync(entry);
        }

        public async Task UpdateAsync(Guid id, UpdateTaskDto dto)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                .Where(e => e.Id == id && e.UserId == uid && !e.IsDeleted)
                .FirstOrDefaultAsync();
            if (entry is null) return;

            entry.Title                = dto.Title ?? entry.Title;
            entry.Note                 = dto.Note;
            entry.StartDateTicks       = dto.StartDate?.Ticks;
            entry.EndDateTicks         = dto.EndDate?.Ticks;
            entry.PlannedDurationTicks = dto.PlannedDurationMinutes.HasValue
                ? TimeSpan.FromMinutes(dto.PlannedDurationMinutes.Value).Ticks
                : entry.PlannedDurationTicks;
            entry.CachedAt             = DateTime.UtcNow;

            await conn.UpdateAsync(entry);
        }

        /// <summary>
        /// Outbox POST başarısından sonra geçici görev Id'sini sunucu Id'si ile değiştirir.
        /// </summary>
        public async Task ReconcileCreateIdAsync(Guid tempId, Guid serverId)
        {
            if (tempId == serverId) return;
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid)) return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                .Where(e => e.Id == tempId && e.UserId == uid)
                .FirstOrDefaultAsync();
            if (entry is null) return;

            await conn.DeleteAsync(entry);
            entry.Id = serverId;
            await conn.InsertOrReplaceAsync(entry);
        }

        public async Task ClearAsync()
        {
            var conn = await db.GetAsync();
            await conn.DeleteAllAsync<TaskCacheEntry>();
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        private static TaskDto ToDto(TaskCacheEntry e) => new()
        {
            Id              = e.Id,
            LessonId        = e.LessonId,
            Title           = e.Title,
            Note            = e.Note,
            Status          = Enum.TryParse<TaskStatus>(e.Status, out var s)
                                ? s : TaskStatus.Pending,
            PlannedDuration = e.PlannedDurationTicks.HasValue
                                ? TimeSpan.FromTicks(e.PlannedDurationTicks.Value) : null,
            StartDate       = e.StartDateTicks.HasValue
                                ? new DateTime(e.StartDateTicks.Value, DateTimeKind.Utc) : null,
            EndDate         = e.EndDateTicks.HasValue
                                ? new DateTime(e.EndDateTicks.Value, DateTimeKind.Utc) : null
        };

        private static TaskCacheEntry ToEntry(TaskDto d, string userId) => new()
        {
            Id                   = d.Id,
            LessonId             = d.LessonId,
            Title                = d.Title,
            Note                 = d.Note,
            Status               = d.Status.ToString(),
            PlannedDurationTicks = d.PlannedDuration?.Ticks,
            StartDateTicks       = d.StartDate?.Ticks,
            EndDateTicks         = d.EndDate?.Ticks,
            CachedAt             = DateTime.UtcNow,
            UserId               = userId,
            IsDeleted            = false
        };
    }
}
