using StudyTime.Domain.Enums;

namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class CategoryTimeDto
    {
        public LessonType Type { get; init; }
        public double Hours { get; init; }
    }
}
