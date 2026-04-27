namespace StudyTime.Application.DTOs.Auth
{
    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? ClientType { get; set; }
        public string? Hwid { get; set; }
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ClientType { get; set; } // "Desktop" or "Mobile"
        public string? Hwid { get; set; } // Hardware ID
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumUntil { get; set; }
        public DateTime Expiration { get; set; }
    }

    public class UpdateProfileRequestDto
    {
        public string? FullName { get; set; }
        // Optional: Could add email change later, but requires re-verify. Let's start with FullName.
    }

    public class ChangePasswordRequestDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class TokenRequestDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
