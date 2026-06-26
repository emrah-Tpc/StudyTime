using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Interfaces;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // F21: Global policy zaten koruyor; diğer controller'larla tutarlılık için explicit.
    public class StatisticsController(
        IStatisticsService statisticsService,
        ICurrentUserService currentUserService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetStatistics([FromQuery] string range = "7days")
        {
            // F11: "bugün"ü kullanıcının saat dilimine (offset) göre hesapla; F21: range tek yerde eşlenir.
            var userToday = DateTime.UtcNow.AddMinutes(currentUserService.UtcOffsetMinutes).Date;
            DateTime endDate = userToday;
            DateTime startDate = range switch
            {
                "30days"  => userToday.AddDays(-29),
                "3months" => userToday.AddMonths(-3),
                _         => userToday.AddDays(-6) // "7days" (varsayılan)
            };

            var stats = await statisticsService.GetStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
    }
}
