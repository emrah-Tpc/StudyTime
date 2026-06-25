namespace StudyTime.Application.Auth
{
    /// <summary>
    /// JWT yapılandırması — secret tek noktadan (Program.cs'te çözülüp) buraya verilir.
    /// Kaynak kodda dağıtılmış hardcoded secret'lar yerine tek doğruluk kaynağı.
    /// </summary>
    public sealed class JwtSettings
    {
        public string Secret { get; init; } = string.Empty;
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public int ExpiryMinutes { get; init; } = 60;
    }
}
