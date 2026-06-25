using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;
using StudyTime.Infrastructure.Repositories;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F03 — REGRESYON: "Soft-delete edilen ders, o derse ait geçmiş StudySession'ları
/// istatistik/dashboard sorgularından düşürmemeli."
///
/// Neden InMemory değil SQLite? Bulgu, EF Core'un ilişkisel sorgu hattındaki
/// (required navigation + global query filter) INNER JOIN davranışına dayanıyor.
/// InMemory provider ilişkisel join üretmez; bug'ı yansıtmaz (yanlış-yeşil).
/// SQLite ilişkisel hattı SQL Server ile aynı INNER/LEFT join kararını verir.
///
/// Faz 0'da bu test mevcut HATALI davranışı (Assert.Empty) belgeliyordu; F03 fix'i
/// (StudySessionRepository.SessionsWithLessonScoped — global filter bypass + manuel
/// izolasyon) sonrası beklenti Assert.Single'a çevrildi. Fix geri alınırsa bu test kırmızıya döner.
/// </summary>
public class SoftDeleteStatisticsTests
{
    [Fact]
    public async Task GetByDateRange_StillReturnsSessions_WhenLessonSoftDeleted_F03()
    {
        var userId = Guid.NewGuid().ToString();

        // SQLite in-memory: bağlantı açık kaldığı sürece şema yaşar.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<StudyTimeDbContext>()
            .UseSqlite(connection)
            .Options;

        // ── Seed (system context: query filter karışmadan kurulum) ───────────────
        await using (var seed = new StudyTimeDbContext(options, new TestUser(userId, isSystem: true)))
        {
            await seed.Database.EnsureCreatedAsync();

            // AppUser FK (StudySession.UserId / Lesson.UserId → AspNetUsers) için kullanıcı satırı.
            seed.Users.Add(new AppUser
            {
                Id = userId,
                UserName = "test@studytime.io",
                NormalizedUserName = "TEST@STUDYTIME.IO",
                Email = "test@studytime.io",
                NormalizedEmail = "TEST@STUDYTIME.IO"
            });
            await seed.SaveChangesAsync();

            var lesson = new Lesson("Matematik", "#3b82f6") { UserId = userId };
            seed.Lessons.Add(lesson);

            var session = new StudySession(lesson.Id) { UserId = userId };
            session.Start();   // StartedAt = UtcNow
            session.Stop();    // EndedAt = UtcNow
            seed.StudySessions.Add(session);

            await seed.SaveChangesAsync();

            // Dersi soft-delete et (oturum SİLİNMEDİ; sadece ders).
            lesson.MarkAsDeleted();
            await seed.SaveChangesAsync();
        }

        // ── Sorgu (gerçek kullanıcı bağlamı: query filter aktif) ──────────────────
        await using (var ctx = new StudyTimeDbContext(options, new TestUser(userId, isSystem: false)))
        {
            var start = DateTime.UtcNow.Date.AddDays(-1);
            var end   = DateTime.UtcNow.Date.AddDays(1);

            // 1) Oturum satırı hâlâ DB'de — silinmedi (yalnızca ders soft-deleted).
            var rawCount = await ctx.StudySessions
                .IgnoreQueryFilters()
                .CountAsync(s => s.UserId == userId);
            Assert.Equal(1, rawCount);

            // 2) Include(Lesson) OLMADAN: oturum kendi query filter'ını geçer ve döner.
            var withoutInclude = await ctx.StudySessions
                .Where(s => s.StartedAt >= start && s.StartedAt <= end)
                .CountAsync();
            Assert.Equal(1, withoutInclude);

            // 3) Repo (Include(Lesson) yapar): F03 fix'i sayesinde ders soft-deleted olsa da
            //    oturum DÜŞMEZ ve silinmiş dersin verisi (Name/Color/Type) yine de gelir.
            var repo = new StudySessionRepository(ctx, new TestUser(userId, isSystem: false));
            var viaRepo = await repo.GetByDateRangeAsync(start, end);

            Assert.Single(viaRepo);
            Assert.NotNull(viaRepo[0].Lesson);
            Assert.Equal("Matematik", viaRepo[0].Lesson!.Name);
        }
    }

    private sealed class TestUser : ICurrentUserService
    {
        public TestUser(string? userId, bool isSystem)
        {
            UserId = userId;
            IsSystemContext = isSystem;
        }

        public string? UserId { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public bool IsSystemContext { get; }
        public string? Email => null;
    }
}
