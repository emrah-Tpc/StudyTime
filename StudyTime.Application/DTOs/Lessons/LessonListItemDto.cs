using StudyTime.Domain.Enums; // Bunu eklemeyi unutma

namespace StudyTime.Application.DTOs.Lessons
{
    public class LessonListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public LessonStatus Status { get; set; }

        // 👇 YENİ EKLENEN ALAN
        public LessonType Type { get; set; }
    }
}