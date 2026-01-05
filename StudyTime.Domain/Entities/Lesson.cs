using StudyTime.Domain.Enums;

namespace StudyTime.Domain.Entities
{
    public class Lesson
    {
        // Properties
        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;
        public string Color { get; private set; } = null!;
        public LessonStatus Status { get; private set; }
        public string? Notes { get; private set; }
        public LessonType Type { get; private set; } = LessonType.Academic;
        // 👇 YENİ: Silindi mi bayrağı
        public bool IsDeleted { get; private set; } = false;

        // Constructor
        public Lesson(string name, string color, LessonType type = LessonType.Academic) // Constructor değişti
        {
            Id = Guid.NewGuid();
            ChangeName(name);
            ChangeColor(color);
            Type = type; // Tipi atıyoruz
            Status = LessonStatus.Active;
            IsDeleted = false;
        }

        public void ChangeName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Lesson name cannot be empty.");

            Name = newName;
        }

        public void ChangeColor(string newColor)  
        {
            if (string.IsNullOrWhiteSpace(newColor))
                throw new ArgumentException("Lesson color cannot be empty.");

            Color = newColor;
        }

        public void Archive()
        {
            if (Status == LessonStatus.Archived)
                throw new InvalidOperationException("Lesson is already archived");
            Status = LessonStatus.Archived;
        }
       
        public void Unarchive()
        {
            if (Status == LessonStatus.Active)
                throw new InvalidOperationException("Lesson is already active.");
            Status = LessonStatus.Active;
        }

        public void UpdateNotes(string newNotes)
        {
            Notes = newNotes;
        }

        // 👇 YENİ: Soft Delete Metodu
        public void MarkAsDeleted()
        {
            if (IsDeleted) return; // Zaten silinmişse işlem yapma
            IsDeleted = true;
        }
    }
}