using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;

namespace StudyTime.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _dashboardService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ISubscriptionAccessService _subscriptionAccessService;

        public DashboardController(
            DashboardService dashboardService,
            ICurrentUserService currentUserService,
            ISubscriptionAccessService subscriptionAccessService)
        {
            _dashboardService = dashboardService;
            _currentUserService = currentUserService;
            _subscriptionAccessService = subscriptionAccessService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(_currentUserService.UserId, out var userId))
            {
                return Unauthorized(new
                {
                    code = "INVALID_USER_CONTEXT",
                    message = "Kimlik doğrulama bağlamı doğrulanamadı."
                });
            }

            var summary = await _dashboardService.GetSummaryAsync();
            var canAccess = await _subscriptionAccessService.CanAccessPremiumFeaturesAsync(userId, cancellationToken);

            if (!canAccess)
            {
                // Demo/GBYF için grafikleri her zaman gösteriyoruz
                // summary.WeeklyChartData = new List<StudyTime.Application.DTOs.Dashboard.ChartDataDto>();
                // summary.DailyChartData = new List<StudyTime.Application.DTOs.Dashboard.ChartDataDto>();
                // summary.CategoryChartData = new List<StudyTime.Application.DTOs.Dashboard.ChartDataDto>();
            }

            return Ok(summary);
        }
    }
}
