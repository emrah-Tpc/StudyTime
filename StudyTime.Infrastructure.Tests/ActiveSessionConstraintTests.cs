using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F06 — Tek-aktif-oturum unique filtered index'in DB seviyesinde gerçekten zorlandığını
/// doğrular. Eşzamanlı iki "start" servis kontrolünü birlikte geçtiğinde insert bu kısıtı
/// ihlal eder ve DbUpdateException fırlatır; controller bunu 409'a eşler.
/// (İlişkisel davranış için SQLite kullanılır; InMemory unique index'i zorlamaz.)
/// </summary>
public class ActiveSessionConstraintTests
{
    [Fact]
    public async Task SecondActiveSession_SameUser_ViolatesUniqueIndex()
    {
        var userId = Guid.NewGuid().ToString();
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<StudyTimeDbContext>().UseSqlite(conn).Options;

        await using var ctx = new StudyTimeDbContext(options, new TestUser(userId));
        await ctx.Database.EnsureCreatedAsync();

        ctx.Users.Add(new AppUser
        {
            Id = userId, UserName = "t@t.io", NormalizedUserName = "T@T.IO",
            Email = "t@t.io", NormalizedEmail = "T@T.IO"
        });
        var lesson = new Lesson("Ders", "#ffffff") { UserId = userId };
        ctx.Lessons.Add(lesson);
        await ctx.SaveChangesAsync();

        var first = new StudySession(lesson.Id) { UserId = userId };
        first.Start(); // aktif (EndedAt null)
        ctx.StudySessions.Add(first);
        await ctx.SaveChangesAsync();

        var second = new StudySession(lesson.Id) { UserId = userId };
        second.Start(); // ikinci aktif oturum → kısıt ihlali
        ctx.StudySessions.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    private sealed class TestUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool IsSystemContext => true;
        public string? Email => null;
    }
}
