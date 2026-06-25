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
        public const string SeedEmail = "demo@studytime.io";
        public const string SeedPassword = "Demo123!";

        public async Task SeedAsync()
        {
            var user = await userManager.FindByEmailAsync(SeedEmail);
            if (user == null)
            {
                user = new AppUser
                {
                    UserName = SeedEmail,
                    Email = SeedEmail,
                    FullName = "GBYF Demo Kullanıcısı",
                    IsPremium = true,
                    PremiumUntil = DateTime.UtcNow.AddYears(5),
                    SubscriptionType = SubscriptionType.Yearly
                };
                var create = await userManager.CreateAsync(user, SeedPassword);
                if (!create.Succeeded)
                    return;
            }

            var userId = user.Id;
            var hasLessons = await db.Lessons.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId);
            if (hasLessons)
            {
                // Mevcut demo verilerini temizle ki her seferinde taze ve tam veri olsun
                await db.StudySessions.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
                await db.Tasks.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
                await db.Lessons.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
            }

            var lessons = new[]
            {
                new Lesson("Matematik Analiz", "#3b82f6", LessonType.Academic) { UserId = userId },
                new Lesson("Algoritma Tasarımı", "#8b5cf6", LessonType.Academic) { UserId = userId },
                new Lesson("Full-Stack Dev", "#10b981", LessonType.Work) { UserId = userId },
                new Lesson("İngilizce C1", "#f59e0b", LessonType.Personal) { UserId = userId },
                // new Lesson("Mühendislik Etiği", "#ef4444", LessonType.Academic) { UserId = userId },
                new Lesson("Sistem Mimarisi", "#06b6d4", LessonType.Work) { UserId = userId },
            };

            db.Lessons.AddRange(lessons);
            await db.SaveChangesAsync();

            var tasks = new List<TaskItem>();
            var now = DateTime.UtcNow;
            var rand = new Random(42);

            for (int i = 0; i < 40; i++)
            {
                var lesson = lessons[rand.Next(lessons.Length)];
                var start = now.Date.AddDays(rand.Next(-30, 7)).AddHours(rand.Next(8, 20));
                var task = new TaskItem(
                    title: i % 4 == 0 ? $"{lesson.Name} Proje Teslimi" : $"{lesson.Name} Çalışma #{i + 1}",
                    lessonId: lesson.Id,
                    startDate: start,
                    endDate: start.AddDays(rand.Next(1, 3)),
                    note: i % 5 == 0 ? "GBYF Yarışması için hazırlık notları. Tüm modüllerin test edilmesi gerekiyor." : null,
                    plannedDuration: TimeSpan.FromMinutes(45 + rand.Next(0, 10) * 15))
                { UserId = userId };

                if (i % 6 == 0) task.Cancel();
                else if (i % 1.5 < 1) task.Complete();

                tasks.Add(task);
            }

            db.Tasks.AddRange(tasks);
            await db.SaveChangesAsync();

            var sessions = new List<StudySession>();
            // Son 45 günü doldur (Heatmap için harika görünür)
            for (int day = 0; day < 45; day++)
            {
                var dayDate = now.Date.AddDays(-day);
                
                // Hafta içi daha yoğun, hafta sonu biraz daha az (gerçekçi görünüm)
                var isWeekend = dayDate.DayOfWeek == DayOfWeek.Saturday || dayDate.DayOfWeek == DayOfWeek.Sunday;
                var sessionCount = isWeekend ? rand.Next(1, 4) : rand.Next(3, 7);

                for (int i = 0; i < sessionCount; i++)
                {
                    var lesson = lessons[rand.Next(lessons.Length)];
                    var linkedTask = tasks.Where(t => t.LessonId == lesson.Id && t.Status != TaskStatus.Cancelled).OrderBy(x => rand.Next()).FirstOrDefault();
                    
                    // Altın Saatler (Peak) oluşturmak için seansları 09:00 - 14:00 arasına kümele
                    int startHour;
                    if (rand.Next(100) < 70) // %70 ihtimalle verimli saatlerde
                        startHour = rand.Next(9, 13);
                    else
                        startHour = rand.Next(14, 22);

                    var start = dayDate.AddHours(startHour).AddMinutes(rand.Next(0, 60));
                    var minutes = 30 + rand.Next(0, 90);
                    var end = start.AddMinutes(minutes);

                    var s = new StudySession(lesson.Id, linkedTask?.Id, isBreak: false) { UserId = userId };
                    s.Start();
                    s.Stop();
                    SetSessionTimes(s, start, end, TimeSpan.FromMinutes(minutes));
                    sessions.Add(s);

                    // Mola ekle
                    if (rand.Next(100) < 80)
                    {
                        var b = new StudySession(lesson.Id, null, isBreak: true) { UserId = userId };
                        b.Start();
                        b.Stop();
                        var bDur = TimeSpan.FromMinutes(10 + rand.Next(0, 10));
                        SetSessionTimes(b, end, end.Add(bDur), bDur);
                        sessions.Add(b);
                    }
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

