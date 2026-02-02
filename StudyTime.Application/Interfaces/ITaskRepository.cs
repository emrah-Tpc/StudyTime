using StudyTime.Domain.Entities;

namespace StudyTime.Application.Interfaces
{
    public interface ITaskRepository
    {
        Task AddAsync(TaskItem task);
        Task<TaskItem?> GetByIdAsync(Guid id);
        Task<List<TaskItem>> GetAllAsync();
        Task UpdateAsync(TaskItem task);
        Task<List<Domain.Entities.TaskItem>> GetByLessonIdAsync(Guid lessonId);
        // Soft delete yapacağımız için Hard Delete metoduna (DeleteAsync) aslında gerek kalmadı
        // ama interface'de durabilir, implementasyonda kullanmayacağız.
        Task DeleteAsync(TaskItem task);

        Task<(List<TaskItem> Items, int TotalCount)> GetFilteredAsync(
           string? status,
           Guid? lessonId,
           string? search,
           int page,
           int pageSize);
    }
}