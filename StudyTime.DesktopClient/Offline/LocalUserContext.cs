namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Yerel SQLite önbelleğinde satır düzeyinde kullanıcı izolasyonu için güncel oturum kullanıcı Id'si (JWT <c>sub</c>).
    /// </summary>
    public sealed class LocalUserContext
    {
        public string? UserId { get; private set; }

        public void SetUserId(string? userId) => UserId = userId;

        public void Clear() => UserId = null;
    }
}
