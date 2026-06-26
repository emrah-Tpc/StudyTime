namespace StudyTime.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? UserId { get; }
        bool IsAuthenticated { get; }
        bool IsSystemContext { get; }
        string? Email { get; }

        /// <summary>
        /// İstemcinin UTC'ye göre dakika cinsinden offset'i (X-Timezone-Offset header'ından).
        /// "bugün"/yerel gün hesaplarında sunucu saat dilimi yerine kullanıcının saat dilimi kullanılır.
        /// Varsayılan 0 (UTC) — header yoksa veya implementasyon override etmezse.
        /// </summary>
        int UtcOffsetMinutes => 0;
    }
}
