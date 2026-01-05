using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LessonController : ControllerBase
    {
        private readonly LessonService _lessonService;

        public LessonController(LessonService lessonService)
        {
            _lessonService = lessonService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _lessonService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLessonDto dto)
        {
            var id = await _lessonService.CreateAsync(dto);
            return Ok(new { lessonId = id });
        }

        // --- YENİ EKLENEN ENDPOINTLER ---

        [HttpPut("{id}/archive")]
        public async Task<IActionResult> Archive(Guid id)
        {
            await _lessonService.ArchiveAsync(id);
            return Ok(new { message = "Workspace archived" });
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> Restore(Guid id)
        {
            await _lessonService.RestoreAsync(id);
            return Ok(new { message = "Workspace restored" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _lessonService.DeleteAsync(id);
            return Ok(new { message = "Workspace deleted" });
        }
    }
}