using StudyTime.Domain.Entities;

namespace StudyTime.Application.Interfaces
{
    public interface IStudySessionRepository
    {
        Task AddAsync(StudySession session);
        Task UpdateAsync(StudySession session);
        Task<StudySession?> GetByIdAsync(Guid id);
        Task<StudySession?> GetActiveByLessonIdAsync(Guid lessonId);
        Task<List<StudySession>> GetByDateAsync(DateTime date);

        // Bu metodu ekledik, Service katmanı bunu kullanıyor:
        Task<List<StudySession>> GetAllAsync();
    }
}