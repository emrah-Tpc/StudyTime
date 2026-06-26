using StudyTime.Application.DTOs.Notifications;
using StudyTime.Controllers;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Repositories;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F16 — Over-posting (mass-assignment) regresyon testi.
/// CreateNotification artık entity yerine <see cref="CreateNotificationDto"/> alır;
/// DTO'da UserId/IsRead/IsDeleted alanları olmadığı için istemci bunları enjekte edemez.
/// Bu test, controller'ın kaydettiği entity'de bu alanların güvenli (sunucu kontrollü)
/// değerlerde olduğunu doğrular.
/// </summary>
public class NotificationOverPostingTests
{
    [Fact]
    public async Task CreateNotification_DoesNotAcceptClientUserIdReadOrDeletedFlags()
    {
        var repo = new CapturingNotificationRepository();
        var controller = new NotificationController(repo);

        var dto = new CreateNotificationDto
        {
            Title    = "Hatırlatma",
            Message  = "Çalışma zamanı",
            Category = "Motivation"
        };

        await controller.CreateNotification(dto);

        Assert.NotNull(repo.Captured);
        // UserId istemciden gelmez; DbContext geçerli kullanıcıya atar → burada null olmalı.
        Assert.Null(repo.Captured!.UserId);
        Assert.False(repo.Captured.IsRead);
        Assert.False(repo.Captured.IsDeleted);
        Assert.Equal("Motivation", repo.Captured.Category);
        Assert.Equal("Hatırlatma", repo.Captured.Title);
        Assert.NotEqual(Guid.Empty, repo.Captured.Id);
    }

    [Fact]
    public async Task CreateNotification_DefaultsCategoryToSystem_WhenEmpty()
    {
        var repo = new CapturingNotificationRepository();
        var controller = new NotificationController(repo);

        await controller.CreateNotification(new CreateNotificationDto { Title = "X", Category = null });

        Assert.Equal("System", repo.Captured!.Category);
    }

    private sealed class CapturingNotificationRepository : INotificationRepository
    {
        public Notification? Captured { get; private set; }

        public Task<Notification> AddAsync(Notification notification)
        {
            Captured = notification;
            return Task.FromResult(notification);
        }

        public Task<IEnumerable<Notification>> GetAllAsync()
            => Task.FromResult<IEnumerable<Notification>>(new List<Notification>());

        public Task<Notification?> GetByIdAsync(Guid id) => Task.FromResult<Notification?>(null);
        public Task UpdateAsync(Notification notification) => Task.CompletedTask;
        public Task MarkAllAsReadAsync() => Task.CompletedTask;
        public Task DeleteOldNotificationsAsync(int daysToKeep) => Task.CompletedTask;
        public Task DeleteAllAsync() => Task.CompletedTask;
    }
}
