using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Services;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// Bug: "Top 5 görev grafiği boş geliyordu." Kök neden — grafik yalnız TAMAMLANMIŞ +
/// TaskId ile başlatılmış oturuma sahip görevleri gösteriyordu. Düzeltme: çalışma süresi
/// olan görevleri (tamamlanma şartı olmadan) süreye göre göster.
/// </summary>
public class TopTasksStatisticsTests
{
    [Fact]
    public async Task TaskStatistics_IncludesNonCompletedTask_WithStudyTime()
    {
        var lessonId = Guid.NewGuid();
        var task = new TaskItem("Analiz", lessonId); // Pending (tamamlanmamış)
        var session = MakeSession(lessonId, task.Id, minutes: 25);

        var service = new StatisticsService(
            new FakeSessionRepo(new List<StudySession> { session }),
            new FakeTaskRepo(new List<TaskItem> { task }),
            new FakeLessonRepo(),
            new ProductivityCalculator());

        var result = await service.GetStatisticsAsync(DateTime.Today.AddDays(-6), DateTime.Today);

        var top = Assert.Single(result.TaskStatistics);
        Assert.Equal("Analiz", top.Title);
        Assert.False(top.IsCompleted);        // tamamlanmamış ama yine de listede
        Assert.True(top.DurationMinutes >= 25);
    }

    private static StudySession MakeSession(Guid lessonId, Guid taskId, int minutes)
    {
        var s = new StudySession(lessonId, taskId, isBreak: false);
        s.Start();
        s.Stop();
        typeof(StudySession).GetProperty("TotalActiveDuration")!.SetValue(s, TimeSpan.FromMinutes(minutes));
        typeof(StudySession).GetProperty("EndedAt")!.SetValue(s, DateTime.UtcNow);
        typeof(StudySession).GetProperty("LastResumedAt")!.SetValue(s, null);
        return s;
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

    private sealed class FakeTaskRepo(List<TaskItem> tasks) : ITaskRepository
    {
        public Task<List<TaskItem>> GetByDateRangeAsync(DateTime s, DateTime e) => Task.FromResult(tasks);
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
