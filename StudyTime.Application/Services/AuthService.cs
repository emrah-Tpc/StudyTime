using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StudyTime.Application.DTOs.Auth;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace StudyTime.Application.Services
{
    public class AuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ISubscriptionAccessService _subscriptionAccessService;

        public AuthService(UserManager<AppUser> userManager, IConfiguration configuration, ISubscriptionAccessService subscriptionAccessService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _subscriptionAccessService = subscriptionAccessService;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                IsPremium = false,
                PremiumUntil = null,
                SubscriptionType = SubscriptionType.Free
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Kayıt başarısız: {errors}");
            }

            return await GenerateTokensAsync(user, request.ClientType, request.Hwid);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                throw new Exception("Geçersiz e-posta veya şifre.");
            }

            // Desktop Login sadece Premium/Pro kullanıcıya izin verecek
            if (request.ClientType == "Desktop")
            {
                if (!Guid.TryParse(user.Id, out var userId))
                {
                    throw new UnauthorizedAccessException("INVALID_USER_CONTEXT");
                }

                bool canUseDesktop = await _subscriptionAccessService.CanUseDesktopAsync(userId, cancellationToken);
                if (!canUseDesktop)
                {
                    throw new UnauthorizedAccessException("DESKTOP_PREMIUM_REQUIRED");
                }
            }

            return await GenerateTokensAsync(user, request.ClientType, request.Hwid);
        }

        public async Task UpdateProfileAsync(string userId, UpdateProfileRequestDto request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("Kullanıcı bulunamadı.");
            }

            if (!string.IsNullOrEmpty(request.FullName))
            {
                user.FullName = request.FullName;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Profil güncellenemedi: {errors}");
            }
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordRequestDto request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("Kullanıcı bulunamadı.");
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Şifre değiştirilemedi: {errors}");
            }
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(TokenRequestDto request)
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                throw new Exception("Geçersiz access token.");
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                throw new Exception("Geçersiz token claimleri.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("Kullanıcı bulunamadı.");
            }

            string? clientType = null;
            string? hwid = null;

            if (user.DesktopRefreshToken == request.RefreshToken && user.DesktopRefreshTokenExpiryTime > DateTime.UtcNow)
            {
                clientType = "Desktop";
                hwid = user.DesktopHwid;
            }
            else if (user.MobileRefreshToken == request.RefreshToken && user.MobileRefreshTokenExpiryTime > DateTime.UtcNow)
            {
                clientType = "Mobile";
                hwid = user.MobileHwid;
            }
            else
            {
                throw new Exception("Geçersiz veya süresi dolmuş refresh token.");
            }

            return await GenerateTokensAsync(user, clientType, hwid);
        }

        public async Task LogoutAsync(string userId, string? hwid)
        {
            if (string.IsNullOrEmpty(hwid)) return;

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                bool updated = false;
                if (user.DesktopHwid == hwid)
                {
                    user.DesktopHwid = null;
                    user.DesktopRefreshToken = null;
                    user.DesktopRefreshTokenExpiryTime = null;
                    updated = true;
                }
                else if (user.MobileHwid == hwid)
                {
                    user.MobileHwid = null;
                    user.MobileRefreshToken = null;
                    user.MobileRefreshTokenExpiryTime = null;
                    updated = true;
                }

                if (updated)
                {
                    await _userManager.UpdateAsync(user);
                }
            }
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrEmpty(secret)) secret = "DevelopmentSuperSecretKeyWhichNeedsToBeAtLeast32BytesLong!";

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateLifetime = false // Süresi geçmiş token kabul edilsin
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<AuthResponseDto> GenerateTokensAsync(AppUser user, string? clientType, string? hwid)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
            
            if (string.IsNullOrEmpty(secret))
            {
                secret = "DevelopmentSuperSecretKeyWhichNeedsToBeAtLeast32BytesLong!";
            }

            var key = Encoding.UTF8.GetBytes(secret);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("FullName", user.FullName ?? ""),
                new Claim("IsPremium", user.IsPremium.ToString()),
                new Claim("PremiumUntil", user.PremiumUntil?.ToString("O") ?? "")
            };

            var expiration = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"]!));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiration,
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            var refreshToken = GenerateRefreshToken();
            
            if (clientType == "Desktop")
            {
                user.DesktopHwid = hwid;
                user.DesktopRefreshToken = refreshToken;
                user.DesktopRefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            }
            else if (clientType == "Mobile" || clientType == "Mac")
            {
                user.MobileHwid = hwid;
                user.MobileRefreshToken = refreshToken;
                user.MobileRefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            }

            await _userManager.UpdateAsync(user);

            return new AuthResponseDto
            {
                Token = tokenHandler.WriteToken(token),
                RefreshToken = refreshToken,
                Email = user.Email!,
                FullName = user.FullName,
                IsPremium = user.IsPremium,
                PremiumUntil = user.PremiumUntil,
                Expiration = expiration
            };
        }
    }
}
