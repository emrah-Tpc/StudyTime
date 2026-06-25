using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class StudySessionRepository(StudyTimeDbContext context, ICurrentUserService currentUser) : IStudySessionRepository
    {
        /// <summary>
        /// F03: StudySession + Lesson, global query filter BYPASS edilerek getirilir.
        /// Lesson soft-delete edilmiş olsa bile (required navigation + filter INNER JOIN
        /// etkileşimi nedeniyle) oturum DÜŞMEZ. Kullanıcı izolasyonu ve soft-delete kontrolü
        /// StudySession üzerinde manuel uygulanır (DbContext'teki filtre mantığının aynısı).
        /// </summary>
        private IQueryable<StudySession> SessionsWithLessonScoped()
        {
            return context.StudySessions
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted &&
                    (currentUser.IsSystemContext ||
                     (!string.IsNullOrWhiteSpace(currentUser.UserId) && s.UserId == currentUser.UserId)))
                .Include(s => s.Lesson)
                .AsNoTracking();
        }

        public async Task AddAsync(StudySession session)
        {
            await context.StudySessions.AddAsync(session);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(StudySession studySession)
        {
            context.StudySessions.Update(studySession);
            await context.SaveChangesAsync();
        }

        public async Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId)
        {
            return await context.StudySessions
                .Where(s => s.LessonId == lessonId && s.EndedAt == null)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<StudySession?> GetActiveSessionAsync(string userId)
        {
            return await context.StudySessions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.EndedAt == null && !s.IsDeleted);
        }

        public async Task<List<StudySession>> GetByDateAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var startUtc = startOfDay.Kind == DateTimeKind.Utc ? startOfDay : startOfDay.ToUniversalTime();
            var endUtc = endOfDay.Kind == DateTimeKind.Utc ? endOfDay : endOfDay.ToUniversalTime();

            return await context.StudySessions
                .Where(s => s.StartedAt >= startUtc && s.StartedAt <= endUtc)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<StudySession?> GetByIdAsync(Guid id)
        {
            return await context.StudySessions
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        // Eklenen Metot:
        public async Task<List<StudySession>> GetAllAsync()
        {
            return await SessionsWithLessonScoped().ToListAsync();
        }
        public async Task<List<StudySession>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var startOfDay = startDate.Date;
            var endOfDay = endDate.Date.AddDays(1).AddTicks(-1);

            var startUtc = startOfDay.Kind == DateTimeKind.Utc ? startOfDay : startOfDay.ToUniversalTime();
            var endUtc = endOfDay.Kind == DateTimeKind.Utc ? endOfDay : endOfDay.ToUniversalTime();

            return await SessionsWithLessonScoped()
                .Where(s => s.StartedAt >= startUtc && s.StartedAt <= endUtc)
                .ToListAsync();
        }
    }
}