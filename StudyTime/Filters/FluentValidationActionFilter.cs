using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StudyTime.Filters
{
    /// <summary>
    /// DI'a kayıtlı (AddValidatorsFromAssembly...) FluentValidation validator'larını
    /// action argümanları için otomatik çalıştırır. Geçersizse action'a girilmeden
    /// 400 ValidationProblem döner.
    /// <br/>
    /// Not: Üçüncü parti auto-validation paketi yerine, sürüm çakışması riskini önlemek
    /// için minimal ve kendi kontrolümüzde tutulan bir filtredir.
    /// </summary>
    public sealed class FluentValidationActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null)
                    continue;

                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
                if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                    continue;

                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
                if (result.IsValid)
                    continue;

                foreach (var error in result.Errors)
                    context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            if (!context.ModelState.IsValid)
            {
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
                return;
            }

            await next();
        }
    }
}
