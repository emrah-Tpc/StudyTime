using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StudyTime.Application.Exceptions;

namespace StudyTime
{
    /// <summary>
    /// F18 — Merkezi hata yönetimi. Beklenen istisnaları uygun HTTP koduna eşler;
    /// beklenmeyenleri 500 + GENERİK mesaja indirger (iç hata/DB mesajı sızdırmaz) ve loglar.
    /// </summary>
    public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title, detail) = Map(exception);

            if (status >= StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Beklenmeyen hata: {Message}", exception.Message);
            else
                logger.LogWarning("İşlenen istisna {Type}: {Message}", exception.GetType().Name, exception.Message);

            var problem = new ProblemDetails
            {
                Status = status,
                Title  = title,
                Detail = detail
            };

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        private static (int Status, string Title, string Detail) Map(Exception ex) => ex switch
        {
            DataConflictException        => (StatusCodes.Status409Conflict,     "Conflict",          ex.Message),
            KeyNotFoundException         => (StatusCodes.Status404NotFound,      "Not Found",         ex.Message),
            ValidationException ve       => (StatusCodes.Status400BadRequest,    "Validation Failed",
                                                string.Join(" ", ve.Errors.Select(e => e.ErrorMessage))),
            ArgumentException            => (StatusCodes.Status400BadRequest,    "Bad Request",       ex.Message),
            InvalidOperationException    => (StatusCodes.Status400BadRequest,    "Bad Request",       ex.Message),
            UnauthorizedAccessException  => (StatusCodes.Status401Unauthorized,  "Unauthorized",      ex.Message),
            _                            => (StatusCodes.Status500InternalServerError, "Server Error",
                                                "Beklenmeyen bir sunucu hatası oluştu.")
        };
    }
}
