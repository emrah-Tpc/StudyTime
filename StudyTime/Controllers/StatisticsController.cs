using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Interfaces;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // F21: Global policy zaten koruyor; diğer controller'larla tutarlılık için explicit.
    public class StatisticsController(IStatisticsService statisticsService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetStatistics([FromQuery] string range = "7days")
        {
            // F21: range değerleri tek yerde, net şekilde başlangıç tarihine eşlenir.
            DateTime endDate = DateTime.Today;
            DateTime startDate = range switch
            {
                "30days"  => DateTime.Today.AddDays(-29),
                "3months" => DateTime.Today.AddMonths(-3),
                _         => DateTime.Today.AddDays(-6) // "7days" (varsayılan)
            };

            var stats = await statisticsService.GetStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
    }
}
