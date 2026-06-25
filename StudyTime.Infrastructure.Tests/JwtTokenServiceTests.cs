using System.Security.Claims;
using StudyTime.Application.Auth;
using StudyTime.Application.Services;
using StudyTime.Domain.Entities;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F20 — JWT secret tekilleştirme + JwtTokenService ayrıştırması.
/// Token üretim/doğrulama tek serviste; UserManager bağımlılığı olmadan test edilebilir.
/// </summary>
public class JwtTokenServiceTests
{
    private static JwtTokenService Create(string? secret = null) => new(new JwtSettings
    {
        Secret = secret ?? "TestSuperSecretKeyWhichNeedsToBeAtLeast32BytesLong!!",
        Issuer = "StudyTime.API",
        Audience = "StudyTime.Clients",
        ExpiryMinutes = 60
    });

    [Fact]
    public void CreateAccessToken_RoundTripsThroughGetPrincipalFromExpiredToken()
    {
        var svc = Create();
        var user = new AppUser { Id = Guid.NewGuid().ToString(), Email = "a@b.com", FullName = "Test", IsPremium = true };

        var (token, expiration) = svc.CreateAccessToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiration > DateTime.UtcNow);

        var principal = svc.GetPrincipalFromExpiredToken(token);
        Assert.Equal(user.Id, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithDifferentSecret_Throws()
    {
        var issuer = Create();
        var user = new AppUser { Id = Guid.NewGuid().ToString(), Email = "a@b.com" };
        var (token, _) = issuer.CreateAccessToken(user);

        var verifierWithWrongSecret = Create("DIFFERENT-Secret-Key-Also-At-Least-32-Bytes!!");

        Assert.ThrowsAny<Exception>(() => verifierWithWrongSecret.GetPrincipalFromExpiredToken(token));
    }

    [Fact]
    public void CreateRefreshToken_IsRandom64Bytes()
    {
        var svc = Create();
        var a = svc.CreateRefreshToken();
        var b = svc.CreateRefreshToken();

        Assert.NotEqual(a, b);
        Assert.Equal(64, Convert.FromBase64String(a).Length);
    }
}
