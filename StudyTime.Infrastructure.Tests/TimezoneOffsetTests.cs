using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Services;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F11 — İstatistik gün-gruplaması sunucu saat dilimi yerine kullanıcının UTC offset'ine göre yapılır.
/// 22:30 UTC'deki bir oturum: offset 0 (UTC) → o gün; offset +120 (UTC+2) → ertesi gün (00:30).
/// </summary>
public class TimezoneOffsetTests
{
    [Fact]
    public async Task DailyTrend_GroupsByUserLocalDay_NotServerTime()
    {
        // 2026-06-20 22:30 UTC, 60 dk çalışma
        var startUtc = new DateTime(2026, 6, 20, 22, 30, 0, DateTimeKind.Utc);
        var session = MakeSession(startUtc, minutes: 60);

        var rangeStart = new DateTime(2026, 6, 19);
        var rangeEnd   = new DateTime(2026, 6, 22);
        // StudyTrends sırası: [0]=19, [1]=20, [2]=21, [3]=22

        var utc = new StatisticsService(
            new FakeSessionRepo(new List<StudySession> { session }),
            new FakeTaskRepo(), new FakeLessonRepo(), new FakeCurrentUser(0), new ProductivityCalculator());
        var rUtc = await utc.GetStatisticsAsync(rangeStart, rangeEnd);

        Assert.True(rUtc.StudyTrends[1].Value >= 60); // 20 Haz (UTC günü)
        Assert.Equal(0, rUtc.StudyTrends[2].Value);    // 21 Haz boş

        var plus2 = new StatisticsService(
            new FakeSessionRepo(new List<StudySession> { session }),
            new FakeTaskRepo(), new FakeLessonRepo(), new FakeCurrentUser(120), new ProductivityCalculator());
        var rPlus2 = await plus2.GetStatisticsAsync(rangeStart, rangeEnd);

        Assert.True(rPlus2.StudyTrends[2].Value >= 60); // UTC+2 → 21 Haz'a kayar
        Assert.Equal(0, rPlus2.StudyTrends[1].Value);    // 20 Haz artık boş
    }

    private static StudySession MakeSession(DateTime startedAtUtc, int minutes)
    {
        var s = new StudySession(Guid.NewGuid(), null, isBreak: false);
        s.Start();
        s.Stop();
        typeof(StudySession).GetProperty("StartedAt")!.SetValue(s, startedAtUtc);
        typeof(StudySession).GetProperty("TotalActiveDuration")!.SetValue(s, TimeSpan.FromMinutes(minutes));
        typeof(StudySession).GetProperty("EndedAt")!.SetValue(s, startedAtUtc.AddMinutes(minutes));
        typeof(StudySession).GetProperty("LastResumedAt")!.SetValue(s, null);
        return s;
    }

    private sealed class FakeCurrentUser(int offset) : ICurrentUserService
    {
        public string? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSystemContext => false;
        public string? Email => null;
        public int UtcOffsetMinutes => offset;
    }

    private sealed class FakeSessionRepo(List<StudySession> sessions) : IStudySessionRepository
    {
        public Task<List<StudySession>> GetByDateRangeAsync(DateTime s, DateTime e) => Task.FromResult(sessions);
        public Task AddAsync(StudySession session) => Task.CompletedTask;
        public Task UpdateAsync(StudySession session) => Task.CompletedTask;
        public Task<StudySession?> GetByIdAsync(Guid id) => Task.FromResult<StudySession?>(null);
        public Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId) => Task.FromResult<StudySession?>(null);
        public Task<StudySession?> GetActiveSessionAsync(string userId) => Task.FromResult<StudySession?>(null);
        public Task<List<StudySession>> GetByDateAsync(DateTime date) => Task.FromResult(new List<StudySession>());
        public Task<List<StudySession>> GetAllAsync() => Task.FromResult(new List<StudySession>());
    }

    private sealed class FakeTaskRepo : ITaskRepository
    {
        public Task<List<TaskItem>> GetByDateRangeAsync(DateTime s, DateTime e) => Task.FromResult(new List<TaskItem>());
        public Task AddAsync(TaskItem task) => Task.CompletedTask;
        public Task<TaskItem?> GetByIdAsync(Guid id) => Task.FromResult<TaskItem?>(null);
        public Task<List<TaskItem>> GetAllAsync() => Task.FromResult(new List<TaskItem>());
        public Task UpdateAsync(TaskItem task) => Task.CompletedTask;
        public Task<List<TaskItem>> GetByLessonIdAsync(Guid lessonId) => Task.FromResult(new List<TaskItem>());
        public Task DeleteAsync(TaskItem task) => Task.CompletedTask;
        public Task<List<TaskItem>> GetPendingTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<(List<TaskItem> Items, int TotalCount)> GetFilteredAsync(
            string? status, Guid? lessonId, string? search, int page, int pageSize)
            => Task.FromResult((new List<TaskItem>(), 0));
    }

    private sealed class FakeLessonRepo : ILessonRepository
    {
        public Task AddAsync(Lesson lesson) => Task.CompletedTask;
        public Task<Lesson?> GetByIdAsync(Guid id) => Task.FromResult<Lesson?>(null);
        public Task<List<Lesson>> GetAllAsync() => Task.FromResult(new List<Lesson>());
        public Task UpdateAsync(Lesson lesson) => Task.CompletedTask;
        public Task DeleteAsync(Lesson lesson) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid id) => Task.FromResult(true);
    }
}
