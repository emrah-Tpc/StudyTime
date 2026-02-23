using StudyTime.Application.DTOs.Lessons;
using StudyTime.Domain.Enums;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="LessonCacheEntry"/> tablosunu yöneten yerel SQLite önbelleği.
    /// Okuma ve yazma işlemleri thread-safe SQLiteAsyncConnection üzerinden yapılır.
    /// </summary>
    public class LocalLessonCache(LocalDb db)
    {
        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>Önbellekteki tüm aktif dersleri döndürür.</summary>
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            var conn    = await db.GetAsync();
            var entries = await conn.Table<LessonCacheEntry>().ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>Id'ye göre önbellekten ders getirir.</summary>
        public async Task<LessonListItemDto?> GetByIdAsync(Guid id)
        {
            var conn  = await db.GetAsync();
            var entry = await conn.Table<LessonCacheEntry>()
                                  .Where(e => e.Id == id)
                                  .FirstOrDefaultAsync();
            return entry is null ? null : ToDto(entry);
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// API'den alınan listeyi önbelleğe yazar (INSERT OR REPLACE).
        /// Eski kayıtların üzerine geçer.
        /// </summary>
        public async Task UpsertAllAsync(IEnumerable<LessonListItemDto> dtos)
        {
            var conn    = await db.GetAsync();
            var entries = dtos.Select(d => ToEntry(d)).ToList();
            
            await conn.RunInTransactionAsync(tran =>
            {
                foreach (var entry in entries)
                {
                    tran.InsertOrReplace(entry);
                }
            });
        }

        /// <summary>Önbellekten tek kayıt siler (lesson silindikten sonra).</summary>
        public async Task DeleteAsync(Guid id)
        {
            var conn = await db.GetAsync();
            await conn.DeleteAsync<LessonCacheEntry>(id);
        }

        /// <summary>Tüm önbelleği temizler (logout / hard refresh).</summary>
        public async Task ClearAsync()
        {
            var conn = await db.GetAsync();
            await conn.DeleteAllAsync<LessonCacheEntry>();
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        private static LessonListItemDto ToDto(LessonCacheEntry e) => new()
        {
            Id     = e.Id,
            Name   = e.Name,
            Color  = e.Color,
            Status = Enum.TryParse<LessonStatus>(e.Status, out var s) ? s : LessonStatus.Active,
            Type   = Enum.TryParse<LessonType>(e.Type, out var t) ? t : LessonType.Academic,
            Notes  = e.Notes
        };

        private static LessonCacheEntry ToEntry(LessonListItemDto d) => new()
        {
            Id       = d.Id,
            Name     = d.Name,
            Color    = d.Color,
            Status   = d.Status.ToString(),
            Type     = d.Type.ToString(),
            Notes    = d.Notes,
            CachedAt = DateTime.UtcNow
        };
    }
}
