using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Infrastructure.Persistence;
using TaskStatus = StudyTime.Domain.Enums.TaskStatus;

namespace StudyTime.Infrastructure.Repositories
{
    public class TaskRepository(StudyTimeDbContext context) : ITaskRepository
    {
        public async Task AddAsync(TaskItem task)
        {
            await context.Tasks.AddAsync(task);
            await context.SaveChangesAsync();
        }

        public async Task<TaskItem?> GetByIdAsync(Guid id)
        {
            return await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<TaskItem>> GetAllAsync()
        {
            // 🚀 OPTİMİZASYON
            return await context.Tasks
                                .AsNoTracking()
                                .Where(t => !t.IsDeleted)
                                .OrderByDescending(t => t.Id)
                                .ToListAsync();
        }

        public async Task UpdateAsync(TaskItem task)
        {
            context.Tasks.Update(task);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(TaskItem task)
        {
            context.Tasks.Remove(task);
            await context.SaveChangesAsync();
        }

        public async Task<(List<TaskItem> Items, int TotalCount)> GetFilteredAsync(
            string? statusStr,
            Guid? lessonId,
            string? search,
            int page,
            int pageSize)
        {
            // 🚀 OPTİMİZASYON: AsNoTracking eklendi
            var query = context.Tasks.AsNoTracking().AsQueryable();

            query = query.Where(t => !t.IsDeleted);

            if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<TaskStatus>(statusStr, out var statusEnum))
            {
                query = query.Where(t => t.Status == statusEnum);
            }

            if (lessonId.HasValue)
            {
                query = query.Where(t => t.LessonId == lessonId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Title.Contains(search) ||
                                         (t.Note != null && t.Note.Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}