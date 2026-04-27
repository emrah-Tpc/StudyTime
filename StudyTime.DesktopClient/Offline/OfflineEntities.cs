using SQLite;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.DTOs.Tasks;

namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// SQLite'ta saklanacak Lesson önbellek kaydı.
    /// LessonListItemDto'nun flat mirror'ı — navigation property yok.
    /// </summary>
    [Table("LessonCache")]
    public class LessonCacheEntry
    {
        [PrimaryKey]
        public Guid   Id       { get; set; }
        public string Name     { get; set; } = string.Empty;
        public string Color    { get; set; } = string.Empty;
        public string Status   { get; set; } = string.Empty;   // enum adı (string)
        public string Type     { get; set; } = string.Empty;   // enum adı (string)
        public string? Notes   { get; set; }

        /// <summary>Bu kaydın API'den son yenilendiği zaman.</summary>
        public DateTime CachedAt { get; set; }

        /// <summary>AspNetUsers Id — sunucu global filtresiyle uyumlu yerel izolasyon.</summary>
        public string? UserId { get; set; }

        /// <summary>Yerel soft delete (1) veya sunucu soft delete yansıması; okumada filtrelenir.</summary>
        public bool IsDeleted { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite'ta saklanacak Task önbellek kaydı.
    /// TaskDto'nun flat mirror'ı — TimeSpan/DateTime alanları Ticks olarak saklanır.
    /// </summary>
    [Table("TaskCache")]
    public class TaskCacheEntry
    {
        [PrimaryKey]
        public Guid    Id                   { get; set; }

        /// <summary>Görevin ait olduğu ders. null ise genel görev.</summary>
        public Guid?   LessonId             { get; set; }

        public string  Title                { get; set; } = string.Empty;
        public string? Note                 { get; set; }

        /// <summary>TaskStatus enum adı (string).</summary>
        public string  Status               { get; set; } = string.Empty;

        /// <summary>TimeSpan.Ticks — SQLite TimeSpan desteği yok.</summary>
        public long?   PlannedDurationTicks { get; set; }

        /// <summary>DateTime.Ticks (UTC) — SQLite DateTime desteği kısıtlı.</summary>
        public long?   StartDateTicks       { get; set; }
        public long?   EndDateTicks         { get; set; }

        public DateTime CachedAt            { get; set; }

        /// <summary>AspNetUsers Id — yerel izolasyon.</summary>
        public string? UserId { get; set; }

        public bool IsDeleted { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Offline iken yapılan yazma işlemleri için outbox kuyruk kaydı.
    /// İnternet geldiğinde <see cref="OutboxProcessor"/> tarafından işlenir.
    /// </summary>
    [Table("OutboxQueue")]
    public class OutboxEntry
    {
        [PrimaryKey, AutoIncrement]
        public int    Id         { get; set; }

        /// <summary>"Lesson" | "Task"</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>"Create" | "Update" | "Delete" | "UpdateNotes" | "Toggle"</summary>
        public string Operation  { get; set; } = string.Empty;

        /// <summary>İşlem payload'ı (JSON string).</summary>
        public string Payload    { get; set; } = string.Empty;

        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public int      RetryCount { get; set; } = 0;
    }

    /// <summary>
    /// Outbox aynı kayıt için maksimum deneme sayısını aştığında taşınan kalıcı kayıt (veri kaybı yok; müdahale için).
    /// </summary>
    [Table("DeadLetterQueue")]
    public class DeadLetterEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string EntityType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
        public DateTime FailedAt { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Offline üretilen yerel oturum Guid'i ile sunucunun döndürdüğü gerçek oturum Id eşlemesi (Stop/Pause/Resume için).
    /// </summary>
    [Table("SessionServerIdMap")]
    public class SessionServerIdMapEntry
    {
        [PrimaryKey]
        public Guid LocalSessionId { get; set; }

        public Guid ServerSessionId { get; set; }
        public DateTime MappedAt { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dashboard / Statistics gibi karmaşık DTO'ların JSON snapshot'ı.
    /// Key: "Dashboard" | "Statistics_week" | "Statistics_month" | "Statistics_year"
    /// </summary>
    [Table("SnapshotCache")]
    public class SnapshotCacheEntry
    {
        /// <summary>Benzersiz anahtar — örn. "Dashboard", "Statistics_week"</summary>
        [PrimaryKey]
        public string Key       { get; set; } = string.Empty;

        /// <summary>Serileştirilmiş DTO (JSON).</summary>
        public string JsonValue { get; set; } = string.Empty;

        /// <summary>API'den son alınan zaman (UI'da "X dakika önce" için).</summary>
        public DateTime CachedAt { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Çalışma oturumu geçmişi için read-only SQLite cache.
    /// StudySession API listesi buraya yansıtılır; offline geçmiş gösterimi için kullanılır.
    /// </summary>
    [Table("StudySessionCache")]
    public class StudySessionCacheEntry
    {
        [PrimaryKey]
        public Guid      Id              { get; set; }
        public Guid      LessonId        { get; set; }
        public Guid?     TaskId          { get; set; }
        public bool      IsBreak         { get; set; }
        public DateTime  StartedAt       { get; set; }
        public DateTime? EndedAt         { get; set; }
        public long      DurationSeconds { get; set; }
        public string    LessonName      { get; set; } = string.Empty;
        public string    LessonColor     { get; set; } = string.Empty;
        public DateTime  CachedAt        { get; set; }

        public string? UserId { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Outbox payload yardımcı tipleri
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Offline'da OutboxQueue'ya yazılan "StudySession/Start" payload'ı.</summary>
    public record StudySessionStartPayload
    {
        public Guid     LocalSessionId { get; init; }
        public Guid     LessonId       { get; init; }
        public Guid?    TaskId         { get; init; }
        public bool     IsBreak        { get; init; }
        public DateTime StartedAt      { get; init; }
    }

    /// <summary>Offline'da OutboxQueue'ya yazılan "StudySession/Stop" payload'ı.</summary>
    public record StudySessionStopPayload
    {
        public Guid     LocalSessionId { get; init; }
        public DateTime StoppedAt      { get; init; }
    }

    /// <summary>Offline'da OutboxQueue'ya yazılan "Task/Update" payload'ı.</summary>
    public record TaskUpdatePayload
    {
        public Guid          Id  { get; init; }
        public UpdateTaskDto Dto { get; init; } = new();
    }

    /// <summary>Task Create outbox — POST sonrası yerel temp Id sunucu Id ile değiştirilir.</summary>
    public record TaskCreateOutboxPayload
    {
        public Guid          ClientTempId { get; init; }
        public CreateTaskDto Dto          { get; init; } = new();
    }

    /// <summary>Lesson Create outbox.</summary>
    public record LessonCreateOutboxPayload
    {
        public Guid           ClientTempId { get; init; }
        public CreateLessonDto Dto         { get; init; } = new();
    }

    /// <summary>Çevrimdışı geçici Guid → sunucu Id (outbox zincirinde Update/Delete çözümü).</summary>
    [Table("TempIdMap")]
    public class TempIdMapEntry
    {
        public string EntityType { get; set; } = string.Empty;

        [PrimaryKey]
        public Guid TempId { get; set; }

        public Guid ServerId { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bildirimler için offline önbellek kaydı.
    /// </summary>
    [Table("NotificationCache")]
    public class NotificationCacheEntry
    {
        [PrimaryKey]
        public Guid     Id        { get; set; }
        public string   Title     { get; set; } = string.Empty;
        public string   Message   { get; set; } = string.Empty;
        public string   Category  { get; set; } = "System";
        public bool     IsRead    { get; set; }
        public string?  ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CachedAt  { get; set; }

        public string? UserId { get; set; }
    }
}
