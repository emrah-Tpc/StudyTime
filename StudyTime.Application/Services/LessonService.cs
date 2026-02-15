using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    // HATA ÇÖZÜMÜ: Sınıf adının yanındaki parantezler (...) kaldırıldı.
    public class LessonService
    {
        private readonly ILessonRepository _lessonRepository;
        private readonly ITaskRepository _taskRepository;

        // Klasik Constructor (Çakışma olmaması için tek yöntem bu olmalı)
        public LessonService(ILessonRepository lessonRepository, ITaskRepository taskRepository)
        {
            _lessonRepository = lessonRepository;
            _taskRepository = taskRepository;
        }

        // CREATE
        public async Task<Guid> CreateAsync(CreateLessonDto dto)
        {
            var lesson = new Lesson(dto.Name, dto.Color, dto.Type);
            await _lessonRepository.AddAsync(lesson);
            return lesson.Id;
        }

        // READ (List)
        public async Task<List<LessonListItemDto>> GetAllAsync()
        {
            var lessons = await _lessonRepository.GetAllAsync();

            return lessons.Select(l => new LessonListItemDto
            {
                Id = l.Id,
                Name = l.Name,
                Color = l.Color,
                Status = l.Status,
                Type = l.Type
            }).ToList();
        }

        // READ (Single)
        public async Task<Lesson?> GetByIdAsync(Guid id)
        {
            return await _lessonRepository.GetByIdAsync(id);
        }

        // WORKSPACE DETAYI (Notlar ve Görevler Dahil)
        // StudyTime.Application/Services/LessonService.cs

        public async Task<WorkspaceDetailDto?> GetWorkspaceDetailAsync(Guid lessonId)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null) return null;

            var tasks = await _taskRepository.GetByLessonIdAsync(lessonId);

            return new WorkspaceDetailDto
            {
                Id = lesson.Id,
                Name = lesson.Name,
                Color = lesson.Color,

                // 👇 BU SATIR EKSİK OLDUĞU İÇİN NOTLAR GELMİYOR! MUTLAKA EKLE 👇
                Note = lesson.Notes,

                Tasks = tasks.Select(t => new TaskListItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Note = t.Note, // Not: Note alanını ekledik
                    IsCompleted = t.Status == Domain.Enums.TaskStatus.Completed
                }).ToList()
            };
        }

        // UPDATE (Notes)
        public async Task UpdateNotesAsync(Guid lessonId, string notes)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson != null)
            {
                lesson.UpdateNotes(notes);
                await _lessonRepository.UpdateAsync(lesson);
            }
        }

        // ARCHIVE
        public async Task ArchiveAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id);
            if (lesson != null)
            {
                lesson.Archive();
                await _lessonRepository.UpdateAsync(lesson);
            }
        }

        // RESTORE
        public async Task RestoreAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id);
            if (lesson != null)
            {
                lesson.Unarchive();
                await _lessonRepository.UpdateAsync(lesson);
            }
        }

        // DELETE (Soft Delete)
        public async Task DeleteAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id);
            if (lesson != null)
            {
                lesson.MarkAsDeleted();
                await _lessonRepository.UpdateAsync(lesson);
            }
        }
    }
}