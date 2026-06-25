using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.StudySessions;
using StudyTime.Application.Services;
using System.Security.Claims;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class StudySessionController : ControllerBase
    {
        private readonly StudySessionService _service;

        public StudySessionController(StudySessionService service)
        {
            _service = service;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Kullanıcı kimliği alınamadı.");

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartStudySessionDto dto)
        {
            try
            {
                var id = await _service.StartAsync(dto, GetUserId());
                return Ok(new { SessionId = id });
            }
            catch (InvalidOperationException ex) when (ex.Message == "ACTIVE_SESSION_EXISTS")
            {
                return Conflict(new { message = "ACTIVE_SESSION_EXISTS" });
            }
            catch (KeyNotFoundException ex) when (ex.Message == "LESSON_NOT_FOUND")
            {
                return BadRequest(new { message = "LESSON_NOT_FOUND" });
            }
        }

        // Pause/Resume/Stop hata eşlemesi: GlobalExceptionHandler (DataConflict→409)
        [HttpPost("{id:guid}/pause")]
        public async Task<IActionResult> Pause(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _service.PauseAsync(id, updatedAt);
            return NoContent();
        }

        [HttpPost("{id:guid}/resume")]
        public async Task<IActionResult> Resume(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _service.ResumeAsync(id, updatedAt);
            return NoContent();
        }

        [HttpPost("{id:guid}/stop")]
        public async Task<IActionResult> Stop(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _service.StopAsync(id, updatedAt);
            return NoContent();
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var session = await _service.GetActiveSessionAsync(GetUserId());
            if (session == null)
            {
                return NotFound(new { message = "Aktif oturum bulunamadı." });
            }
            return Ok(session);
        }

        [HttpGet("today-total")]
        public async Task<IActionResult> TodayTotal()
        {
            var result = await _service.GetTodayTotalAsync();
            return Ok(result);
        }
    }
}
