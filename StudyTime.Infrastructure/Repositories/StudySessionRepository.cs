using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class StudySessionRepository(StudyTimeDbContext context) : IStudySessionRepository
    {
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

        public async Task<List<StudySession>> GetByDateAsync(DateTime date)
        {
            return await context.StudySessions
                .Where(s => s.StartedAt.Date == date.Date)
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
            return await context.StudySessions
                .AsNoTracking()
                .ToListAsync();
        }
    }
}