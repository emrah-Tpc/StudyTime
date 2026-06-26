using Microsoft.AspNetCore.Http;
using StudyTime.Application.Interfaces;
using System.Globalization;
using System.Security.Claims;

namespace StudyTime.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISystemContextState _systemContextState;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, ISystemContextState systemContextState)
        {
            _httpContextAccessor = httpContextAccessor;
            _systemContextState = systemContextState;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        public bool IsSystemContext => _systemContextState.IsSystemContext;

        public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

        public int UtcOffsetMinutes
        {
            get
            {
                var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Timezone-Offset"].FirstOrDefault();
                // Mantıklı sınırlar: -14h..+14h. Geçersizse 0 (UTC).
                if (int.TryParse(header, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                    && minutes >= -840 && minutes <= 840)
                {
                    return minutes;
                }
                return 0;
            }
        }
    }
}
