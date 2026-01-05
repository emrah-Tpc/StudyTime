using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class StudySessionRepository : IStudySessionRepository
    {
        private readonly StudyTimeDbContext _context;

        public StudySessionRepository(StudyTimeDbContext context)
        {
            _context = context;
        }

        // ✅ Interface: AddAsync
        public async Task AddAsync(StudySession session)
        {
            await _context.StudySessions.AddAsync(session);
            await _context.SaveChangesAsync();
        }

        // ✅ Interface: UpdateAsync
        public async Task UpdateAsync(StudySession studySession)
        {
            _context.StudySessions.Update(studySession);
            await _context.SaveChangesAsync();
        }

        // ✅ Interface: GetActiveByLessonIdAsync
        public async Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId)
        {
            return await _context.StudySessions
                .Where(s => s.LessonId == lessonId && s.EndedAt == null)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();
        }
        public async Task<List<StudySession>> GetByDateAsync(DateTime date)
        {
            return await _context.StudySessions
                .Where(s =>
                    s.StartedAt != DateTime.MinValue &&
                    s.StartedAt.Date == date)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task<StudySession?> GetByIdAsync(Guid id)
        {
            return await _context.StudySessions
                .FirstOrDefaultAsync(s => s.Id == id);
        }

    }
}
