using System;

namespace StudyTime.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsDeleted { get; set; }
        public string Category { get; set; } = "System"; // Discipline, Motivation, Awareness, System
        public string? ActionUrl { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Auth Properties
        public string? UserId { get; set; }
        public AppUser? User { get; set; }
    }
}
