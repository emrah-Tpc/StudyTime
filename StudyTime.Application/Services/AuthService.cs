using Microsoft.AspNetCore.Identity;
using StudyTime.Application.DTOs.Auth;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Enums;
using System.Security.Claims;

namespace StudyTime.Application.Services
{
    public class AuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtTokenService _jwtTokenService;
        private readonly ISubscriptionAccessService _subscriptionAccessService;

        public AuthService(UserManager<AppUser> userManager, JwtTokenService jwtTokenService, ISubscriptionAccessService subscriptionAccessService)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
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
            var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);
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

            // F31: DB'de hash saklanıyor; gelen token'ı hash'leyip karşılaştır.
            var hashedIncoming = JwtTokenService.HashToken(request.RefreshToken);

            if (user.DesktopRefreshToken == hashedIncoming && user.DesktopRefreshTokenExpiryTime > DateTime.UtcNow)
            {
                clientType = "Desktop";
                hwid = user.DesktopHwid;
            }
            else if (user.MobileRefreshToken == hashedIncoming && user.MobileRefreshTokenExpiryTime > DateTime.UtcNow)
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

        private async Task<AuthResponseDto> GenerateTokensAsync(AppUser user, string? clientType, string? hwid)
        {
            var (token, expiration) = _jwtTokenService.CreateAccessToken(user);
            var refreshToken = _jwtTokenService.CreateRefreshToken();

            // F31: İstemciye ham token döner; DB'ye yalnızca hash'i yazılır.
            var hashedRefreshToken = JwtTokenService.HashToken(refreshToken);

            if (clientType == "Desktop")
            {
                user.DesktopHwid = hwid;
                user.DesktopRefreshToken = hashedRefreshToken;
                user.DesktopRefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            }
            else if (clientType == "Mobile" || clientType == "Mac")
            {
                user.MobileHwid = hwid;
                user.MobileRefreshToken = hashedRefreshToken;
                user.MobileRefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            }

            await _userManager.UpdateAsync(user);

            return new AuthResponseDto
            {
                Token = token,
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
