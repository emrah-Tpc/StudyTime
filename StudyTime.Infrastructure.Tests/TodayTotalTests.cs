using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
using StudyTime.Domain.Entities;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F04 — today-total mola sürelerini çalışmaya katmamalı ve CurrentDuration kullanmalı.
/// </summary>
public class TodayTotalTests
{
    [Fact]
    public async Task GetTodayTotal_ExcludesBreaks_AndUsesActiveDuration()
    {
        var work  = MakeStopped(isBreak: false, minutes: 30);
        var brk   = MakeStopped(isBreak: true,  minutes: 15);

        var service = new StudySessionService(
            new FakeSessionRepo(new List<StudySession> { work, brk }),
            new FakeLessonRepo());

        var result = await service.GetTodayTotalAsync();

        Assert.Equal(30, result.TotalMinutes); // mola (15dk) hariç
    }

    private static StudySession MakeStopped(bool isBreak, int minutes)
    {
        var s = new StudySession(Guid.NewGuid(), null, isBreak);
        s.Start();
        s.Stop();
        // CurrentDuration == TotalActiveDuration (EndedAt dolu olduğunda).
        Set(s, "TotalActiveDuration", TimeSpan.FromMinutes(minutes));
        Set(s, "EndedAt", DateTime.UtcNow);
        Set(s, "LastResumedAt", null);
        return s;
    }

    private static void Set(StudySession s, string prop, object? val)
        => typeof(StudySession).GetProperty(prop)!.SetValue(s, val);

    private sealed class FakeSessionRepo(List<StudySession> sessions) : IStudySessionRepository
    {
        public Task<List<StudySession>> GetByDateAsync(DateTime date) => Task.FromResult(sessions);
        public Task AddAsync(StudySession session) => Task.CompletedTask;
        public Task UpdateAsync(StudySession session) => Task.CompletedTask;
        public Task<StudySession?> GetByIdAsync(Guid id) => Task.FromResult<StudySession?>(null);
        public Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId) => Task.FromResult<StudySession?>(null);
        public Task<StudySession?> GetActiveSessionAsync(string userId) => Task.FromResult<StudySession?>(null);
        public Task<List<StudySession>> GetAllAsync() => Task.FromResult(new List<StudySession>());
        public Task<List<StudySession>> GetByDateRangeAsync(DateTime s, DateTime e) => Task.FromResult(new List<StudySession>());
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
