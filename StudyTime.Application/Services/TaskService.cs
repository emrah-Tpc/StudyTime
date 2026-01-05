using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.DTOs.Common;

namespace StudyTime.Application.Services
{
    public class TaskService
    {
        private readonly ITaskRepository _taskRepository;

        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        // CREATE
        public async Task<Guid> CreateTaskAsync(string title, Guid? lessonId, DateTime? startDate, DateTime? endDate, string? note, TimeSpan? plannedDuration)
        {
            var task = new TaskItem(title, lessonId, startDate, endDate, note, plannedDuration);
            await _taskRepository.AddAsync(task);
            return task.Id;
        }

        // UPDATE
        public async Task UpdateTaskAsync(Guid taskId, UpdateTaskDto dto)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task is null) throw new InvalidOperationException("Task not found.");

            task.ChangeTitle(dto.Title);
            task.UpdateDates(dto.StartDate, dto.EndDate);
            task.UpdateNote(dto.Note);
            if (dto.PlannedDurationMinutes.HasValue)
                task.UpdatePlannedDuration(TimeSpan.FromMinutes(dto.PlannedDurationMinutes.Value));
            task.AssignLesson(dto.LessonId);

            await _taskRepository.UpdateAsync(task);
        }

        // COMPLETE
        public async Task CompleteTaskAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task is null) throw new InvalidOperationException("Task not found.");

            task.Complete();
            await _taskRepository.UpdateAsync(task);
        }

        // CANCEL
        public async Task CancelTaskAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task is null) throw new InvalidOperationException("Task not found.");

            task.Cancel();
            await _taskRepository.UpdateAsync(task);
        }

        // REOPEN
        public async Task ReopenTaskAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task is null) throw new InvalidOperationException("Task not found.");

            task.Reopen();
            await _taskRepository.UpdateAsync(task);
        }

        // 👇 SOFT DELETE İŞLEMİ (Buraya Dikkat!)
        public async Task DeleteTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task is null) throw new InvalidOperationException("Task not found.");

            // 1. TaskItem içindeki Delete metodunu çağır (IsDeleted = true yapar)
            task.Delete();

            // 2. Veritabanından silmek yerine GÜNCELLİYORUZ
            await _taskRepository.UpdateAsync(task);
        }

        // GET ALL
        public async Task<List<TaskDto>> GetAllTasksAsync()
        {
            var tasks = await _taskRepository.GetAllAsync();
            return tasks.Select(t => new TaskDto
            {
                Id = t.Id,
                LessonId = t.LessonId,
                Title = t.Title,
                Note = t.Note,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                PlannedDuration = t.PlannedDuration,
                Status = t.Status
            }).ToList();
        }

        // FILTERED LIST
        public async Task<PagedResultDto<TaskListItemDto>> GetTasksAsync(TaskQueryDto query)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

            var (tasks, totalCount) = await _taskRepository.GetFilteredAsync(
                query.Status, query.LessonId, query.Search, page, pageSize);

            var items = tasks.Select(t => new TaskListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Status = t.Status,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                PlannedDurationMinutes = t.PlannedDuration.HasValue ? (int)t.PlannedDuration.Value.TotalMinutes : null,
                LessonId = t.LessonId
            }).ToList();

            return new PagedResultDto<TaskListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // GET BY ID
        public async Task<TaskDetailDto?> GetTaskByIdAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task is null) return null;

            return new TaskDetailDto
            {
                Id = task.Id,
                Title = task.Title,
                Note = task.Note,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                PlannedDurationMinutes = task.PlannedDuration.HasValue ? (int)task.PlannedDuration.Value.TotalMinutes : null,
                Status = task.Status,
                LessonId = task.LessonId
            };
        }
    }
}