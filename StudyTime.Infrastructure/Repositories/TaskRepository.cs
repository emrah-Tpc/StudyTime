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
            // Global Query Filter WHERE IsDeleted = 0 otomatik ekler
            return await context.Tasks
                .AsNoTracking()
                .Include(t => t.Lesson)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        // 👇 DÜZELTİLEN METOT (Workspace Sayfası İçin)
        public async Task<List<TaskItem>> GetByLessonIdAsync(Guid lessonId)
        {
            // Global Query Filter WHERE IsDeleted = 0 otomatik ekler
            return await context.Tasks
                .AsNoTracking()
                .Where(t => t.LessonId == lessonId)
                .OrderByDescending(t => t.StartDate)
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
            var query = context.Tasks.AsNoTracking().AsQueryable();
            // Global Query Filter WHERE IsDeleted = 0 otomatik, ek Where gereksiz

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
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
        public async Task<List<TaskItem>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            // Global Query Filter WHERE IsDeleted = 0 otomatik ekler
            // StartDate veya EndDate aralık içindeyse veya UpdatedAt/CreatedAt aralık içindeyse görev döner
            return await context.Tasks
                .AsNoTracking()
                .Include(t => t.Lesson)
                .Where(t =>
                    // StartDate aralık içinde
                    (t.StartDate.HasValue &&
                     t.StartDate.Value.Date >= startDate.Date &&
                     t.StartDate.Value.Date <= endDate.Date)
                    ||
                    // EndDate aralık içinde (StartDate null olsa bile)
                    (t.EndDate.HasValue &&
                     t.EndDate.Value.Date >= startDate.Date &&
                     t.EndDate.Value.Date <= endDate.Date)
                    ||
                    // Yeni eklendi: Eğer start/end seçilmemiş ama o tarihte bitirilmiş/güncellenmişse (Top 5 için gerekli)
                    (t.UpdatedAt.HasValue &&
                     t.UpdatedAt.Value.Date >= startDate.Date &&
                     t.UpdatedAt.Value.Date <= endDate.Date)
                    ||
                    (t.CreatedAt.Date >= startDate.Date &&
                     t.CreatedAt.Date <= endDate.Date)
                )
                .OrderByDescending(t => t.StartDate ?? t.EndDate ?? t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync();
        }
    }
}