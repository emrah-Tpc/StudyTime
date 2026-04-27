using Microsoft.AspNetCore.Http;
using StudyTime.Application.Interfaces;
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
    }
}
