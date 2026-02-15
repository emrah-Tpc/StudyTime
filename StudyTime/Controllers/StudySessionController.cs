using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.StudySessions;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudySessionController : ControllerBase
    {
        private readonly StudySessionService _service;

        public StudySessionController(StudySessionService service)
        {
            _service = service;
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartStudySessionDto dto)
        {
            var id = await _service.StartAsync(dto);
            return Ok(new { SessionId = id });
        }
        // StudyTime.Controllers.StudySessionController.cs içine ekle:

        [HttpPost("{id:guid}/pause")]
        public async Task<IActionResult> Pause(Guid id)
        {
            await _service.PauseAsync(id);
            return NoContent();
        }

        [HttpPost("{id:guid}/resume")]
        public async Task<IActionResult> Resume(Guid id)
        {
            await _service.ResumeAsync(id);
            return NoContent();
        }
        [HttpPost("{id:guid}/stop")]
        public async Task<IActionResult> Stop(Guid id)
        {
            await _service.StopAsync(id);
            return NoContent();
        }

        [HttpGet("today-total")]
        public async Task<IActionResult> TodayTotal()
        {
            var result = await _service.GetTodayTotalAsync();
            return Ok(result);
        }
    }
}
