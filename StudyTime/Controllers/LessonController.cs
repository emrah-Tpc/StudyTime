using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Interfaces;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/lessons")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class LessonController : ControllerBase
    {
        private readonly LessonService _lessonService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ISubscriptionAccessService _subscriptionAccessService;

        public LessonController(
            LessonService lessonService,
            ICurrentUserService currentUserService,
            ISubscriptionAccessService subscriptionAccessService)
        {
            _lessonService = lessonService;
            _currentUserService = currentUserService;
            _subscriptionAccessService = subscriptionAccessService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _lessonService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLessonDto dto, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(_currentUserService.UserId, out var userId))
            {
                return Unauthorized(new
                {
                    code = "INVALID_USER_CONTEXT",
                    message = "Kimlik doğrulama bağlamı doğrulanamadı."
                });
            }

            // FREE TIER LIMIT: Max 5 Lessons
            bool canAccessPremium = await _subscriptionAccessService.CanAccessPremiumFeaturesAsync(userId, cancellationToken);

            if (!canAccessPremium)
            {
                var existingLessons = await _lessonService.GetAllAsync();
                if (existingLessons.Count >= 5)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        code = "PREMIUM_REQUIRED",
                        message = "Bu özelliği kullanmak için aktif Premium veya Pro abonelik gereklidir."
                    });
                }
            }

            // Servis ID dönüyor, bunu frontend'e iletiyoruz
            var id = await _lessonService.CreateAsync(dto);
            return Ok(new { lessonId = id }); // Frontend bu yanıtı bekliyor muhtemelen
        }

        // ... Diğer metodlar aynı kalabilir (Archive, Restore, Delete)
        // Onların rotaları da artık otomatik olarak "api/lessons/{id}/archive" olacaktır.

        [HttpPut("{id}/archive")]
        public async Task<IActionResult> Archive(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _lessonService.ArchiveAsync(id, updatedAt);
            return Ok(new { message = "Workspace archived" });
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> Restore(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _lessonService.RestoreAsync(id, updatedAt);
            return Ok(new { message = "Workspace restored" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] DateTime? updatedAt = null)
        {
            await _lessonService.DeleteAsync(id, updatedAt);
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

        // 👇 YENİ: Not güncelleme endpoint'i
        [HttpPut("{id}/notes")]
        public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] string notes, [FromQuery] DateTime? updatedAt = null)
        {
            await _lessonService.UpdateNotesAsync(id, notes, updatedAt);
            return Ok(new { message = "Notes updated" });
        }
    }
}