namespace StudyTime.Application.DTOs.Notifications
{
    /// <summary>
    /// Bildirim oluşturma girişi. UYARI: <c>UserId</c>, <c>IsRead</c> ve <c>IsDeleted</c>
    /// alanları KASITLI olarak yoktur — over-posting (mass-assignment) ile başka kullanıcıya
    /// veya silinmiş/okunmuş durumuna müdahale engellenir. Bu değerler sunucuda atanır.
    /// </summary>
    public sealed class CreateNotificationDto
    {
        /// <summary>İstemci geçici Id'si (offline reconciliation). Boşsa sunucu üretir.</summary>
        public Guid? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>Discipline, Motivation, Awareness, System. Boşsa "System".</summary>
        public string? Category { get; set; }

        public string? ActionUrl { get; set; }

        /// <summary>İstemci oluşturma zamanı (offline). Boşsa sunucu UtcNow atar.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
