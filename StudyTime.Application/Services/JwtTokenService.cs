using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StudyTime.Application.Auth;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    /// <summary>
    /// JWT access token üretimi, refresh token üretimi ve süresi geçmiş token'dan
    /// principal çıkarma işlemlerini TEK noktada toplar. AuthService'ten ayrıştırıldığı için
    /// UserManager bağımlılığı olmadan test edilebilir.
    /// </summary>
    public sealed class JwtTokenService
    {
        private readonly JwtSettings _settings;

        public JwtTokenService(JwtSettings settings)
        {
            _settings = settings;
        }

        public (string Token, DateTime Expiration) CreateAccessToken(AppUser user)
        {
            var key = Encoding.UTF8.GetBytes(_settings.Secret);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("FullName", user.FullName ?? string.Empty),
                new("IsPremium", user.IsPremium.ToString()),
                new("PremiumUntil", user.PremiumUntil?.ToString("O") ?? string.Empty)
            };

            var expiration = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiration,
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);
            return (handler.WriteToken(token), expiration);
        }

        public string CreateRefreshToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Refresh token'ı DB'de düz metin yerine saklamak için SHA-256 hash'i.
        /// Token yüksek entropili (64 rastgele bayt) olduğundan tuzsuz SHA-256 yeterlidir.
        /// DB sızsa bile saklanan değer doğrudan kullanılamaz.
        /// </summary>
        public static string HashToken(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var parameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret)),
                ValidateLifetime = false // Süresi geçmiş token kabul edilir (refresh akışı)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwt)
                throw new SecurityTokenException("Invalid token");

            var alg = jwt.Header.Alg;
            var isAllowedAlg =
                string.Equals(alg, SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(alg, SecurityAlgorithms.HmacSha256Signature, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(alg, "HS256", StringComparison.InvariantCultureIgnoreCase);

            if (!isAllowedAlg)
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }
}
