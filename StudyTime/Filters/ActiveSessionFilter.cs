using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudyTime.Domain.Entities;
using System.Security.Claims;

namespace StudyTime.Filters
{
    public class ActiveSessionFilter : IAsyncActionFilter
    {
        private readonly UserManager<AppUser> _userManager;

        public ActiveSessionFilter(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Eğer istek Authenticated değilse (Örn: Login, Register vs.) es geç.
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            // Authenticated isteklerde X-Hardware-Id zorunludur.
            if (!context.HttpContext.Request.Headers.TryGetValue("X-Hardware-Id", out var hardwareIdHeader))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "DEVICE_ID_REQUIRED", details = "Cihaz kimliği (X-Hardware-Id) eksik." });
                return;
            }

            var incomingHwid = hardwareIdHeader.FirstOrDefault();
            
            if (string.IsNullOrEmpty(incomingHwid))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "DEVICE_ID_REQUIRED", details = "Cihaz kimliği (X-Hardware-Id) boş olamaz." });
                return;
            }

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                await next();
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Kullanıcı bulunamadı." });
                return;
            }

            // Gelen HWID, kullanıcının Desktop veya Mobile cihazlarından biriyle eşleşmeli
            bool isDesktopSession = !string.IsNullOrEmpty(user.DesktopHwid) && user.DesktopHwid == incomingHwid;
            bool isMobileSession = !string.IsNullOrEmpty(user.MobileHwid) && user.MobileHwid == incomingHwid;

            if (!isDesktopSession && !isMobileSession)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "SESSION_MISMATCH", details = "Oturumunuz başka bir cihazda açıldığı için sonlandırıldı." });
                return;
            }

            // Her şey yolundaysa isteğe devam et
            await next();
        }
    }
}
