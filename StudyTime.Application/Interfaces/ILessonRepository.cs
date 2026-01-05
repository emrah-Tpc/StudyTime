using StudyTime.Domain.Entities;

namespace StudyTime.Application.Interfaces
{
    public interface ILessonRepository
    {
        Task AddAsync(Lesson lesson);
        Task<Lesson?> GetByIdAsync(Guid id);
        Task<List<Lesson>> GetAllAsync();

        // 👇 Bunları eklemen şart
        Task UpdateAsync(Lesson lesson);
        Task DeleteAsync(Lesson lesson);
    }
}