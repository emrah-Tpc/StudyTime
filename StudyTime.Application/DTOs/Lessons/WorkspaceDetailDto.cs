using StudyTime.Application.DTOs.Tasks; // TaskListItemDto buradan gelecek

namespace StudyTime.Application.DTOs.Lessons
{
    public class WorkspaceDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        // Mevcut DTO'nu kullanıyoruz
        public List<TaskListItemDto> Tasks { get; set; } = new();
    }
}