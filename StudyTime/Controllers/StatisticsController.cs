using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Interfaces;

namespace StudyTime.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController(IStatisticsService statisticsService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetStatistics([FromQuery] string range = "7days")
        {
            DateTime startDate = DateTime.Today.AddDays(-6); // Default 7 days
            DateTime endDate = DateTime.Today;

            if (range == "30days")
            {
                startDate = DateTime.Today.AddDays(-29);
            }
            else if (range == "3months")
            {
                startDate = DateTime.Today.AddMonths(-3);
            }

            var stats = await statisticsService.GetStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
    }
}
