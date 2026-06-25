using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyTime.Application.DTOs.Auth;
using StudyTime.Application.Services;
using System.Security.Claims;

namespace StudyTime.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request, HttpContext.RequestAborted);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                if (ex.Message == "INVALID_USER_CONTEXT")
                {
                    return Unauthorized(new
                    {
                        code = "INVALID_USER_CONTEXT",
                        message = "Kimlik doğrulama bağlamı doğrulanamadı."
                    });
                }

                if (ex.Message == "DESKTOP_PREMIUM_REQUIRED")
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        code = "DESKTOP_PREMIUM_REQUIRED",
                        message = "Masaustu erisimi icin aktif Premium veya Pro abonelik gereklidir."
                    });
                }

                return Unauthorized(new
                {
                    code = "UNAUTHORIZED",
                    message = string.IsNullOrWhiteSpace(ex.Message) ? "Yetkisiz istek." : ex.Message
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                var hwid = Request.Headers["X-Hardware-Id"].FirstOrDefault();
                
                await _authService.LogoutAsync(userId!, hwid);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
        {
            try
            {
                var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                await _authService.UpdateProfileAsync(userId!, request);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            try
            {
                var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                await _authService.ChangePasswordAsync(userId!, request);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
