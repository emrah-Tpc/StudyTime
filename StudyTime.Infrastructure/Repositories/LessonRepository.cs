using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;

namespace StudyTime.Infrastructure.Repositories
{
    public class LessonRepository : ILessonRepository
    {
        private readonly StudyTimeDbContext _context;

        public LessonRepository(StudyTimeDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Lesson lesson)
        {
            await _context.Lessons.AddAsync(lesson);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Lesson>> GetAllAsync()
        {
            // 🚀 OPTİMİZASYON: AsNoTracking()
            // Sadece okuma yaparken EF Core'un takip mekanizmasını kapatır.
            // Workspace sayfasının açılışını hızlandırır.
            return await _context.Lessons
                                 .AsNoTracking()
                                 .Where(l => !l.IsDeleted)
                                 .ToListAsync();
        }

        public async Task<Lesson?> GetByIdAsync(Guid id)
        {
            // Detay sayfasında düzenleme yapılabileceği için burada Tracking açık kalabilir
            // veya performans için kapatıp Update anında Attach yapılabilir. 
            // Şimdilik güvenli olması için normal bırakıyoruz.
            return await _context.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task UpdateAsync(Lesson lesson)
        {
            _context.Lessons.Update(lesson);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Lesson lesson)
        {
            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
        }
    }
}