namespace StudyTime.Application.DTOs.Notifications
{
    /// <summary>
    /// Bildirim yanıt modeli. Domain entity'sini (UserId/IsDeleted gibi iç alanlar)
    /// istemciye sızdırmamak için kullanılır.
    /// </summary>
    public sealed class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = "System";
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
