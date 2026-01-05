using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    public class LessonService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
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
            // Repository zaten filtrelenmiş (silinmemiş) listeyi getirecek
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

        // 👇 DEĞİŞTİ: DELETE (Artık Soft Delete Yapıyor)
        public async Task DeleteAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id);
            if (lesson != null)
            {
                // Veritabanından silmek yerine, "Silindi" olarak işaretle
                lesson.MarkAsDeleted();

                // Durumu güncelle (UpdateAsync kullanıyoruz, DeleteAsync değil!)
                await _lessonRepository.UpdateAsync(lesson);
            }
        }
    }
}