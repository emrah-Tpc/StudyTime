using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/lessons")] // 👈 DÜZELTME: [controller] yerine "api/lessons" yazdık.
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
            // Servis ID dönüyor, bunu frontend'e iletiyoruz
            var id = await _lessonService.CreateAsync(dto);
            return Ok(new { lessonId = id }); // Frontend bu yanıtı bekliyor muhtemelen
        }

        // ... Diğer metodlar aynı kalabilir (Archive, Restore, Delete)
        // Onların rotaları da artık otomatik olarak "api/lessons/{id}/archive" olacaktır.

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

        // 👇 BU METOT EKSİKTİ, BUNU EKLE:
        [HttpGet("{id}/workspace")]
        public async Task<IActionResult> GetWorkspaceDetail(Guid id)
        {
            // Service katmanındaki metodu çağırıyoruz
            var result = await _lessonService.GetWorkspaceDetailAsync(id);

            if (result == null)
                return NotFound("Ders bulunamadı.");

            return Ok(result);
        }
    }
}