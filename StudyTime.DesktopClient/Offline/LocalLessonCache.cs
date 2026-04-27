using StudyTime.Application.DTOs.Lessons;
using StudyTime.Domain.Enums;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// <see cref="LessonCacheEntry"/> tablosunu yöneten yerel SQLite önbelleği.
    /// Okuma ve yazma işlemleri thread-safe SQLiteAsyncConnection üzerinden yapılır.
    /// Sorgular <see cref="LocalUserContext.UserId"/> ve <see cref="LessonCacheEntry.IsDeleted"/> ile izole edilir.
    /// </summary>
    public class LocalLessonCache(LocalDb db, LocalUserContext userContext)
    {
        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>Önbellekteki, geçerli kullanıcıya ait silinmemiş dersleri döndürür.</summary>
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return new List<LessonListItemDto>();

            var conn    = await db.GetAsync();
            var entries = await conn.Table<LessonCacheEntry>()
                .Where(e => e.UserId == uid && !e.IsDeleted)
                .ToListAsync();
            return entries.Select(ToDto).ToList();
        }

        /// <summary>Id'ye göre önbellekten ders getirir (kullanıcı ve silinme filtresi).</summary>
        public async Task<LessonListItemDto?> GetByIdAsync(Guid id)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return null;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<LessonCacheEntry>()
                .Where(e => e.Id == id && e.UserId == uid && !e.IsDeleted)
                .FirstOrDefaultAsync();
            return entry is null ? null : ToDto(entry);
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// API'den alınan listeyi mevcut kullanıcı için yazar (INSERT OR REPLACE).
        /// </summary>
        public async Task UpsertAllAsync(IEnumerable<LessonListItemDto> dtos)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn    = await db.GetAsync();
            var entries = dtos.Select(d => ToEntry(d, uid)).ToList();

            await conn.RunInTransactionAsync(tran =>
            {
                foreach (var entry in entries)
                    tran.InsertOrReplace(entry);
            });
        }

        /// <summary>
        /// Tam liste senkronu: sunucudaki doğruluk için önce bu kullanıcıya ait ders satırlarını siler, sonra API listesini yazar.
        /// (Sunucuda silinmiş derslerin hayalet olarak kalmasını önler.)
        /// </summary>
        public async Task ReplaceAllAsync(IEnumerable<LessonListItemDto> dtos)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            await conn.ExecuteAsync("DELETE FROM LessonCache WHERE UserId = ?", uid);
            await UpsertAllAsync(dtos);
        }

        /// <summary>Önbellekten tek kayıt siler (fiziksel silme).</summary>
        public async Task DeleteAsync(Guid id)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn = await db.GetAsync();
            await conn.ExecuteAsync("DELETE FROM LessonCache WHERE Id = ? AND UserId = ?", id, uid);
        }

        /// <summary>Yerel soft delete — ağ yokken tutarlılık için isteğe bağlı kullanım.</summary>
        public async Task SoftDeleteAsync(Guid id)
        {
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid))
                return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<LessonCacheEntry>()
                .Where(e => e.Id == id && e.UserId == uid)
                .FirstOrDefaultAsync();
            if (entry is null) return;
            entry.IsDeleted = true;
            await conn.UpdateAsync(entry);
        }

        /// <summary>Çevrimdışı not güncellemesi — optimistik yerel tutarlılık.</summary>
        public async Task UpdateNotesLocalAsync(Guid lessonId, string? notes)
        {
            var existing = await GetByIdAsync(lessonId);
            if (existing == null) return;
            existing.Notes = notes;
            await UpsertAllAsync(new[] { existing });
        }

        /// <summary>Outbox POST başarısından sonra geçici ders Id'sini sunucu Id'si ile değiştirir.</summary>
        public async Task ReconcileCreateIdAsync(Guid tempId, Guid serverId)
        {
            if (tempId == serverId) return;
            var uid = userContext.UserId;
            if (string.IsNullOrEmpty(uid)) return;

            var conn  = await db.GetAsync();
            var entry = await conn.Table<LessonCacheEntry>()
                .Where(e => e.Id == tempId && e.UserId == uid)
                .FirstOrDefaultAsync();
            if (entry is null) return;

            await conn.DeleteAsync(entry);
            entry.Id = serverId;
            await conn.InsertOrReplaceAsync(entry);
        }

        /// <summary>Tüm önbelleği temizler (logout / wipe).</summary>
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

        private static LessonCacheEntry ToEntry(LessonListItemDto d, string userId) => new()
        {
            Id        = d.Id,
            Name      = d.Name,
            Color     = d.Color,
            Status    = d.Status.ToString(),
            Type      = d.Type.ToString(),
            Notes     = d.Notes,
            CachedAt  = DateTime.UtcNow,
            UserId    = userId,
            IsDeleted = false
        };
    }
}
