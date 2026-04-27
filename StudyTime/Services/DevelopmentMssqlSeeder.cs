using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Enums;
using StudyTime.Infrastructure.Persistence;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Services
{
    /// <summary>
    /// Development ortamında MSSQL'e test kullanıcısı ve grafikleri dolduracak veri basar.
    /// </summary>
    public sealed class DevelopmentMssqlSeeder(
        UserManager<AppUser> userManager,
        StudyTimeDbContext db)
    {
        public const string SeedEmail = "dev.mssql@studytime.local";
        public const string SeedPassword = "Test123!";

        public async Task SeedAsync()
        {
            var user = await userManager.FindByEmailAsync(SeedEmail);
            if (user == null)
            {
                user = new AppUser
                {
                    UserName = SeedEmail,
                    Email = SeedEmail,
                    FullName = "MSSQL Test User",
                    IsPremium = true,
                    PremiumUntil = DateTime.UtcNow.AddYears(2),
                    SubscriptionType = SubscriptionType.Yearly
                };
                var create = await userManager.CreateAsync(user, SeedPassword);
                if (!create.Succeeded)
                    return;
            }

            var userId = user.Id;
            var hasLessons = await db.Lessons.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId);
            var hasTasks = await db.Tasks.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId);
            var hasSessions = await db.StudySessions.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId);
            if (hasLessons || hasTasks || hasSessions)
                return;

            var lessons = new[]
            {
                new Lesson("Matematik", "#3b82f6", LessonType.Academic) { UserId = userId },
                new Lesson("Yazılım", "#8b5cf6", LessonType.Work) { UserId = userId },
                new Lesson("İngilizce", "#10b981", LessonType.Personal) { UserId = userId },
                new Lesson("Fizik", "#f59e0b", LessonType.Academic) { UserId = userId },
            };

            db.Lessons.AddRange(lessons);
            await db.SaveChangesAsync();

            var tasks = new List<TaskItem>();
            var now = DateTime.UtcNow;
            for (int i = 0; i < 24; i++)
            {
                var lesson = lessons[i % lessons.Length];
                var start = now.Date.AddDays(-(i % 20)).AddHours(8 + (i % 7));
                var end = start.AddHours(1);
                var task = new TaskItem(
                    title: $"{lesson.Name} görev #{i + 1}",
                    lessonId: lesson.Id,
                    startDate: start,
                    endDate: end,
                    note: i % 3 == 0 ? "MSSQL test verisi" : null,
                    plannedDuration: TimeSpan.FromMinutes(25 + (i % 5) * 10))
                { UserId = userId };

                if (i % 5 == 0) task.Cancel();
                else if (i % 2 == 0) task.Complete();

                tasks.Add(task);
            }

            db.Tasks.AddRange(tasks);
            await db.SaveChangesAsync();

            var rand = new Random(42);
            var sessions = new List<StudySession>();
            for (int day = 0; day < 35; day++)
            {
                var dayDate = now.Date.AddDays(-day);
                var sessionCount = 1 + (day % 3);
                for (int i = 0; i < sessionCount; i++)
                {
                    var lesson = lessons[(day + i) % lessons.Length];
                    var linkedTask = tasks.FirstOrDefault(t => t.LessonId == lesson.Id && t.Status != TaskStatus.Cancelled);
                    var start = dayDate.AddHours(7 + i * 3);
                    var minutes = 25 + rand.Next(0, 46);
                    var end = start.AddMinutes(minutes);

                    var s = new StudySession(lesson.Id, linkedTask?.Id, isBreak: false) { UserId = userId };
                    s.Start();
                    s.Stop();
                    SetSessionTimes(s, start, end, TimeSpan.FromMinutes(minutes));
                    sessions.Add(s);

                    var breakStart = end;
                    var breakEnd = breakStart.AddMinutes(10 + rand.Next(0, 6));
                    var b = new StudySession(lesson.Id, null, isBreak: true) { UserId = userId };
                    b.Start();
                    b.Stop();
                    SetSessionTimes(b, breakStart, breakEnd, breakEnd - breakStart);
                    sessions.Add(b);
                }
            }

            db.StudySessions.AddRange(sessions);
            await db.SaveChangesAsync();
        }

        private static void SetSessionTimes(StudySession session, DateTime startedAt, DateTime endedAt, TimeSpan total)
        {
            SetPrivate(session, "StartedAt", startedAt);
            SetPrivate(session, "EndedAt", endedAt);
            SetPrivate(session, "TotalActiveDuration", total);
            SetPrivate(session, "LastResumedAt", null);
        }

        private static void SetPrivate(StudySession session, string propertyName, object? value)
        {
            var prop = typeof(StudySession).GetProperty(propertyName);
            prop?.SetValue(session, value);
        }
    }
}
