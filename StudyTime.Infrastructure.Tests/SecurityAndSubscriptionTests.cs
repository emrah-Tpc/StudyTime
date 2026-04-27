using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Enums;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Tests;

public class SecurityAndSubscriptionTests
{
    [Fact]
    public async Task QueryFilter_ReturnsOnlyCurrentUserData_WhenAuthenticated()
    {
        var userA = Guid.NewGuid().ToString();
        var userB = Guid.NewGuid().ToString();
        await using var context = CreateContext(userA, isSystemContext: false);

        context.Lessons.Add(new Lesson("Math", "#111111") { UserId = userA });
        context.Lessons.Add(new Lesson("Physics", "#222222") { UserId = userB });
        await context.SaveChangesAsync();

        var lessons = await context.Lessons.ToListAsync();

        Assert.Single(lessons);
        Assert.All(lessons, x => Assert.Equal(userA, x.UserId));
    }

    [Fact]
    public async Task QueryFilter_ReturnsNoTenantData_WhenUserIdMissingAndNotSystem()
    {
        var userA = Guid.NewGuid().ToString();
        await using var context = CreateContext(userId: null, isSystemContext: false);

        context.Lessons.Add(new Lesson("Math", "#111111") { UserId = userA });
        await context.SaveChangesAsync();

        var lessons = await context.Lessons.ToListAsync();

        Assert.Empty(lessons);
    }

    [Fact]
    public async Task QueryFilter_SystemContextCanReadAllNonDeletedTenantData()
    {
        var userA = Guid.NewGuid().ToString();
        var userB = Guid.NewGuid().ToString();
        await using var context = CreateContext(userId: null, isSystemContext: true);

        var activeLesson = new Lesson("Math", "#111111") { UserId = userA };
        var deletedLesson = new Lesson("Physics", "#222222") { UserId = userB };
        deletedLesson.MarkAsDeleted();

        context.Lessons.Add(activeLesson);
        context.Lessons.Add(deletedLesson);
        await context.SaveChangesAsync();

        var lessons = await context.Lessons.ToListAsync();

        Assert.Single(lessons);
        Assert.Equal(activeLesson.Id, lessons[0].Id);
    }

    [Theory]
    [InlineData(true, SubscriptionType.Lifetime, null, true)]
    [InlineData(true, SubscriptionType.Monthly, 10, true)]
    [InlineData(true, SubscriptionType.Yearly, -1, false)]
    [InlineData(true, SubscriptionType.Free, 10, false)]
    [InlineData(false, SubscriptionType.Monthly, 10, false)]
    public void HasActivePremium_UsesCentralRules(
        bool isPremium,
        SubscriptionType subscriptionType,
        int? daysOffset,
        bool expected)
    {
        var user = new AppUser
        {
            IsPremium = isPremium,
            SubscriptionType = subscriptionType,
            PremiumUntil = daysOffset.HasValue ? DateTime.UtcNow.AddDays(daysOffset.Value) : null
        };

        var result = user.HasActivePremium(DateTime.UtcNow);

        Assert.Equal(expected, result);
    }

    private static StudyTimeDbContext CreateContext(string? userId, bool isSystemContext)
    {
        var options = new DbContextOptionsBuilder<StudyTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new TestCurrentUserService(userId, isSystemContext);
        return new StudyTimeDbContext(options, currentUser);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(string? userId, bool isSystemContext)
        {
            UserId = userId;
            IsSystemContext = isSystemContext;
        }

        public string? UserId { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public bool IsSystemContext { get; }
        public string? Email => null;
    }
}
