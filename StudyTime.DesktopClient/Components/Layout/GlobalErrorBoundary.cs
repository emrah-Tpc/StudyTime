using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace StudyTime.DesktopClient.Components.Layout;

public sealed class GlobalErrorBoundary : ErrorBoundary
{
    [Inject] private ILogger<GlobalErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled UI exception in Blazor component tree.");
        return Task.CompletedTask;
    }
}

