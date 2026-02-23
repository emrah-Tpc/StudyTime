using StudyTime.Application.DTOs.Tasks;
using StudyTime.Domain.Enums;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="TaskCacheEntry"/> tablosunu yöneten yerel SQLite önbelleği.
    /// <br/>
    /// <b>Okuma:</b> Online → API → cache refresh. Offline → SQLite cache.<br/>
    /// <b>Yazma:</b> Optimistik UI güncellemesi + OutboxQueue üzerinden API replay.
    /// </summary>
    public class LocalTaskCache(LocalDb db)
    {
        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>Önbellekteki tüm görevleri döndürür.</summary>
        public async Task<List<TaskDto>> GetAllAsync()
        {
            var conn    = await db.GetAsync();
            var entries = await conn.Table<TaskCacheEntry>().ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>Belirli bir derse ait görevleri döndürür.</summary>
        public async Task<List<TaskDto>> GetByLessonIdAsync(Guid lessonId)
        {
            var conn    = await db.GetAsync();
            var entries = await conn.Table<TaskCacheEntry>()
                                    .Where(e => e.LessonId == lessonId)
                                    .ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>
        /// Tarih aralığındaki görevleri döndürür.
        /// StartDate VEYA EndDate aralık içindeyse (OR) eşleşir.
        /// Her ikisi de null olan görevler dahil edilmez.
        /// </summary>
        public async Task<List<TaskDto>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            long startTicks = start.Ticks;
            long endTicks   = end.Ticks;

            var conn    = await db.GetAsync();
            // SQLite-net LINQ OR sorgusunu doğrudan desteklemez — hepsini çek, bellek içi filtrele
            var entries = await conn.Table<TaskCacheEntry>().ToListAsync();

            return entries
                .Where(e =>
                    // StartDate aralık içinde
                    (e.StartDateTicks.HasValue &&
                     e.StartDateTicks.Value >= startTicks &&
                     e.StartDateTicks.Value <= endTicks)
                    ||
                    // EndDate aralık içinde
                    (e.EndDateTicks.HasValue &&
                     e.EndDateTicks.Value >= startTicks &&
                     e.EndDateTicks.Value <= endTicks))
                .Select(ToDto)
                .ToList();
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// API'den alınan listeyi önbelleğe yazar (INSERT OR REPLACE).
        /// Eski kayıtların üzerine geçer.
        /// </summary>
        public async Task UpsertAllAsync(IEnumerable<TaskDto> dtos)
        {
            var conn    = await db.GetAsync();
            var entries = dtos.Select(ToEntry).ToList();
            Console.WriteLine($"[TaskCache] UpsertAllAsync: {entries.Count} kayıt yazılıyor...");

            try
            {
                await conn.RunInTransactionAsync(tran =>
                {
                    foreach (var entry in entries)
                        tran.InsertOrReplace(entry);
                });
                Console.WriteLine($"[TaskCache] UpsertAllAsync: {entries.Count} kayıt başarıyla yazıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TaskCache] UpsertAllAsync HATA: {ex.Message}");
                // Fallback: tek tek insert dene
                foreach (var entry in entries)
                {
                    try { await conn.InsertOrReplaceAsync(entry); }
                    catch (Exception ex2) { Console.WriteLine($"[TaskCache] InsertOrReplace HATA ({entry.Id}): {ex2.Message}"); }
                }
            }
        }

        /// <summary>Önbellekten tek kayıt siler.</summary>
        public async Task DeleteAsync(Guid id)
        {
            var conn = await db.GetAsync();
            await conn.DeleteAsync<TaskCacheEntry>(id);
        }

        /// <summary>
        /// Lokal olarak görevin durumunu Completed ↔ Open arasında geçirir.
        /// API replay <see cref="OutboxProcessor"/> tarafından yapılır.
        /// </summary>
        public async Task ToggleCompleteAsync(Guid id)
        {
            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                                  .Where(e => e.Id == id)
                                  .FirstOrDefaultAsync();
            if (entry is null) return;

            entry.Status = entry.Status == TaskStatus.Completed.ToString()
                ? TaskStatus.Pending.ToString()
                : TaskStatus.Completed.ToString();

            await conn.UpdateAsync(entry);
        }

        /// <summary>
        /// Lokal olarak görev alan bilgilerini günceller (optimistik UI).
        /// API replay OutboxProcessor tarafından yapılır.
        /// </summary>
        public async Task UpdateAsync(Guid id, UpdateTaskDto dto)
        {
            var conn  = await db.GetAsync();
            var entry = await conn.Table<TaskCacheEntry>()
                                  .Where(e => e.Id == id)
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

        /// <summary>Tüm önbelleği temizler (logout / hard refresh).</summary>
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

        private static TaskCacheEntry ToEntry(TaskDto d) => new()
        {
            Id                   = d.Id,
            LessonId             = d.LessonId,
            Title                = d.Title,
            Note                 = d.Note,
            Status               = d.Status.ToString(),
            PlannedDurationTicks = d.PlannedDuration?.Ticks,
            StartDateTicks       = d.StartDate?.Ticks,
            EndDateTicks         = d.EndDate?.Ticks,
            CachedAt             = DateTime.UtcNow
        };
    }
}
