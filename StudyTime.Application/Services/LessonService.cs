using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    public class LessonService
    {
        private readonly ILessonRepository _lessonRepository;
        private readonly ITaskRepository _taskRepository; // 👇 EKLENDİ: Görevleri çekmek için gerekli

        // Constructor'a ITaskRepository eklendi
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

        // 👇 YENİ METOT: Workspace Sayfası İçin Detay Getir
        public async Task<WorkspaceDetailDto?> GetWorkspaceDetailAsync(Guid lessonId)
        {
            // 1. Dersi Getir
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null) return null;

            // 2. O derse ait görevleri getir
            // (ITaskRepository içinde GetByLessonIdAsync olduğunu varsayıyoruz)
            var tasks = await _taskRepository.GetByLessonIdAsync(lessonId);

            // 3. DTO Oluştur ve Döndür
            return new WorkspaceDetailDto
            {
                Id = lesson.Id,
                Name = lesson.Name,
                Color = lesson.Color,
                // Task entity'lerini TaskListItemDto'ya çeviriyoruz
                Tasks = tasks.Select(t => new TaskListItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    IsCompleted = t.Status == Domain.Enums.TaskStatus.Completed
                    // Eğer DTO'da başka alanlar varsa (Priority, DueDate vb.) buraya ekle
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