using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyTime.Application.DTOs.Notifications;
using StudyTime.Application.Interfaces;
using StudyTime.Controllers;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;
using StudyTime.Infrastructure.Repositories;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F08 (cleanup soft-delete + UtcNow), F46 (DeleteAll endpoint) ve F17 (GetNotifications DTO).
/// </summary>
public class NotificationCleanupTests
{
    [Fact]
    public async Task DeleteAllAsync_SoftDeletes_CurrentUserNotifications_F46()
    {
        var userId = Guid.NewGuid().ToString();
        var options = InMemoryOptions();

        await using (var ctx = new StudyTimeDbContext(options, new TestCurrentUser(userId)))
        {
            ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), Title = "a", UserId = userId, CreatedAt = DateTime.UtcNow });
            ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), Title = "b", UserId = userId, CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync();

            await new NotificationRepository(ctx).DeleteAllAsync();
        }

        await using (var ctx = new StudyTimeDbContext(options, new TestCurrentUser(userId)))
        {
            // Query filter soft-delete'leri gizler
            Assert.Empty(await ctx.Notifications.ToListAsync());
            // Ama satırlar duruyor (hard-delete değil) ve IsDeleted = true
            var raw = await ctx.Notifications.IgnoreQueryFilters().Where(n => n.UserId == userId).ToListAsync();
            Assert.Equal(2, raw.Count);
            Assert.All(raw, n => Assert.True(n.IsDeleted));
        }
    }

    [Fact]
    public async Task DeleteOldNotificationsAsync_SoftDeletesOnlyOld_F08()
    {
        var userId = Guid.NewGuid().ToString();
        var options = InMemoryOptions();

        await using (var ctx = new StudyTimeDbContext(options, new TestCurrentUser(userId)))
        {
            ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), Title = "old", UserId = userId, CreatedAt = DateTime.UtcNow.AddDays(-40) });
            ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), Title = "recent", UserId = userId, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            await ctx.SaveChangesAsync();

            await new NotificationRepository(ctx).DeleteOldNotificationsAsync(30);
        }

        await using (var ctx = new StudyTimeDbContext(options, new TestCurrentUser(userId)))
        {
            var visible = await ctx.Notifications.ToListAsync();
            Assert.Single(visible);
            Assert.Equal("recent", visible[0].Title);
        }
    }

    [Fact]
    public async Task GetNotifications_ReturnsDtos_NotEntities_F17()
    {
        var note = new Notification
        {
            Id = Guid.NewGuid(), Title = "Bildirim", Message = "Mesaj",
            Category = "Motivation", IsRead = false, UserId = "owner-secret", CreatedAt = DateTime.UtcNow
        };
        var controller = new NotificationController(new StubRepository(note));

        var result = await controller.GetNotifications();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<NotificationDto>>(ok.Value);
        var dto = Assert.Single(dtos);
        Assert.Equal("Bildirim", dto.Title);
        // NotificationDto'da UserId/IsDeleted alanı yok → yapısal olarak sızdırılamaz.
        Assert.DoesNotContain("UserId", typeof(NotificationDto).GetProperties().Select(p => p.Name));
    }

    private static DbContextOptions<StudyTimeDbContext> InMemoryOptions() =>
        new DbContextOptionsBuilder<StudyTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class TestCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public bool IsSystemContext => false;
        public string? Email => null;
    }

    private sealed class StubRepository(Notification toReturn) : INotificationRepository
    {
        public Task<IEnumerable<Notification>> GetAllAsync()
            => Task.FromResult<IEnumerable<Notification>>(new[] { toReturn });

        public Task<Notification?> GetByIdAsync(Guid id) => Task.FromResult<Notification?>(null);
        public Task<Notification> AddAsync(Notification notification) => Task.FromResult(notification);
        public Task UpdateAsync(Notification notification) => Task.CompletedTask;
        public Task MarkAllAsReadAsync() => Task.CompletedTask;
        public Task DeleteOldNotificationsAsync(int daysToKeep) => Task.CompletedTask;
        public Task DeleteAllAsync() => Task.CompletedTask;
    }
}
