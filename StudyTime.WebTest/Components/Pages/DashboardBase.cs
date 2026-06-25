using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StudyTime.Application.DTOs.Dashboard;
using StudyTime.DesktopClient.Services;
using StudyTime.DesktopClient.Offline;

namespace StudyTime.WebTest.Components.Pages;

public abstract class DashboardBase : ComponentBase, IDisposable
{
    [Inject] protected SyncedDashboardApiService DashboardService { get; set; } = default!;
    [Inject] protected SyncedTaskApiService TaskService { get; set; } = default!;
    [Inject] protected GlobalTimerService GlobalTimerService { get; set; } = default!;
    [Inject] protected SyncStatusService SyncStatusService { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    protected DashboardSummaryDto? summary;
    protected bool isLoading = true;
    protected string selectedChart = "Weekly";

    public event Func<Task>? OnChartDataRefreshed;

    protected override async Task OnInitializedAsync()
    {
        GlobalTimerService.OnTimerFinished += OnTimerFinished;
        GlobalTimerService.OnBreakFinished += OnTimerFinished;
        GlobalTimerService.OnTimerStopped  += OnTimerStopped;
        GlobalTimerService.OnTick          += OnGlobalTick;
        SyncStatusService.OnChange         += OnSyncStatusChanged;
        await LoadAsync();
    }

    private DateTime _lastTickUi = DateTime.MinValue;
    private DateTime _lastSoftReloadFromTick = DateTime.UtcNow;

    protected void OnGlobalTick()
    {
        if (!GlobalTimerService.IsRunning || GlobalTimerService.IsBreak) return;
        var now = DateTime.UtcNow;
        if ((now - _lastTickUi).TotalMilliseconds < 900) return;
        _lastTickUi = now;
        InvokeAsync(StateHasChanged);
        if ((now - _lastSoftReloadFromTick).TotalSeconds >= 8)
        {
            _lastSoftReloadFromTick = now;
            InvokeAsync(SoftReload);
        }
    }

    protected int DisplayTodayStudiedMinutes => GlobalTimerService.DisplayTodayStudiedMinutes;

    protected void OnTimerFinished() => InvokeAsync(SoftReloadAndRefreshCharts);
    private void OnTimerStopped() => InvokeAsync(SoftReloadAndRefreshCharts);
    protected void OnSyncStatusChanged() => InvokeAsync(SoftReload);

    public virtual void Dispose()
    {
        GlobalTimerService.OnTimerFinished -= OnTimerFinished;
        GlobalTimerService.OnBreakFinished -= OnTimerFinished;
        GlobalTimerService.OnTimerStopped  -= OnTimerStopped;
        GlobalTimerService.OnTick          -= OnGlobalTick;
        SyncStatusService.OnChange         -= OnSyncStatusChanged;
    }

    protected async Task Reload()
    {
        isLoading = true;
        summary = null;
        StateHasChanged();
        await LoadAsync();
    }

    protected async Task SoftReload()
    {
        try
        {
            var newSummary = await DashboardService.GetSummaryAsync();
            if (newSummary != null) ApplySummary(newSummary);
        }
        catch { }
        finally { StateHasChanged(); }
    }

    private async Task SoftReloadAndRefreshCharts()
    {
        try
        {
            var newSummary = await DashboardService.GetSummaryAsync();
            if (newSummary != null) ApplySummary(newSummary);
        }
        catch { }
        finally
        {
            StateHasChanged();
            if (OnChartDataRefreshed != null)
            {
                try { await OnChartDataRefreshed.Invoke(); }
                catch { }
            }
        }
    }

    protected async Task LoadAsync()
    {
        try
        {
            var cached = await DashboardService.TryGetCachedSummaryAsync();
            if (cached != null)
            {
                summary = cached;
                GlobalTimerService.SetSummaryBaseline(cached.TodayStudiedMinutes);
                isLoading = false;
                StateHasChanged();
            }

            var fresh = await DashboardService.GetSummaryAsync();
            if (fresh != null) ApplySummary(fresh);
        }
        catch { }
        finally { isLoading = false; }
    }

    private void ApplySummary(DashboardSummaryDto newSummary)
    {
        GlobalTimerService.MergeApiSummaryMinutes(newSummary.TodayStudiedMinutes);

        var displayMinutes = GlobalTimerService.DisplayTodayStudiedMinutes;
        if (displayMinutes > newSummary.TodayStudiedMinutes)
            newSummary.TodayStudiedMinutes = displayMinutes;

        summary = newSummary;
    }

    protected void SetChart(string chartType) => selectedChart = chartType;

    protected List<ChartDataDto> GetActiveChartData()
    {
        if (summary == null) return new();
        return selectedChart == "Daily" ? summary.DailyChartData : summary.WeeklyChartData;
    }

    protected string GetHours(int minutes) =>
        minutes < 60 ? $"{minutes}dk" : $"{Math.Round(minutes / 60.0, 1)}s";

    protected string GetProdStatus() => summary?.ProductivityScore >= 80 ? "Mükemmel" :
                                      summary?.ProductivityScore >= 50 ? "İyi Gidiyor" : "Gayret Gerek";

    protected string GetProdStatusClass() => summary?.ProductivityScore >= 80 ? "excellent" :
                                           summary?.ProductivityScore >= 50 ? "good" : "needs-work";
}
