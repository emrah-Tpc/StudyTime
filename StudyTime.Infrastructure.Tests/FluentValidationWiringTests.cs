using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StudyTime.Application.DTOs.Auth;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.Validators.Auth;
using StudyTime.Application.Validators.Tasks;
using StudyTime.Filters;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F01 — FluentValidation artık gerçekten çalışıyor (validator'lar ölü kod değil).
/// Hem validator kurallarını hem de FluentValidationActionFilter'ın geçersiz girişte
/// action'a girmeden 400 döndürdüğünü doğrular.
/// </summary>
public class FluentValidationWiringTests
{
    // ── Validator kuralları ──────────────────────────────────────────────────

    [Fact]
    public void CreateTaskDtoValidator_RejectsEmptyTitleAndOverLongDuration()
    {
        var v = new CreateTaskDtoValidator();

        Assert.False(v.Validate(new CreateTaskDto { Title = "" }).IsValid);
        Assert.False(v.Validate(new CreateTaskDto { Title = "x", PlannedDurationMinutes = 2000 }).IsValid);
        Assert.False(v.Validate(new CreateTaskDto
        {
            Title = "x",
            StartDate = new DateTime(2026, 6, 10),
            EndDate = new DateTime(2026, 6, 1)
        }).IsValid);

        Assert.True(v.Validate(new CreateTaskDto { Title = "Geçerli", PlannedDurationMinutes = 60 }).IsValid);
    }

    [Fact]
    public void RegisterRequestDtoValidator_RejectsInvalidEmailAndShortPassword()
    {
        var v = new RegisterRequestDtoValidator();

        Assert.False(v.Validate(new RegisterRequestDto { Email = "not-an-email", Password = "123456" }).IsValid);
        Assert.False(v.Validate(new RegisterRequestDto { Email = "a@b.com", Password = "123" }).IsValid);
        Assert.True(v.Validate(new RegisterRequestDto { Email = "a@b.com", Password = "123456" }).IsValid);
    }

    // ── Filtre wiring (validator'ların gerçekten tetiklendiği) ────────────────

    [Fact]
    public async Task Filter_ShortCircuitsWith400_OnInvalidDto()
    {
        var ctx = BuildContext(new CreateTaskDto { Title = "" });
        var filter = new FluentValidationActionFilter();
        var nextCalled = false;

        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        Assert.IsType<BadRequestObjectResult>(ctx.Result);
    }

    [Fact]
    public async Task Filter_CallsNext_OnValidDto()
    {
        var ctx = BuildContext(new CreateTaskDto { Title = "Geçerli görev" });
        var filter = new FluentValidationActionFilter();
        var nextCalled = false;

        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, ctx.Filters, ctx.Controller));
        });

        Assert.True(nextCalled);
        Assert.Null(ctx.Result);
    }

    private static ActionExecutingContext BuildContext(object dto)
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<CreateTaskDto>, CreateTaskDtoValidator>();

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["dto"] = dto },
            controller: new object());
    }
}
