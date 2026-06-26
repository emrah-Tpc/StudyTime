using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StudyTime.Application;
using StudyTime.Application.DTOs.Dashboard;
using StudyTime.DesktopClient.Services;
using StudyTime.DesktopClient.Offline;
using System.Globalization;

namespace StudyTime.DesktopClient.Components.Pages;

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
    protected string? loadError;
    protected string selectedChart = "Weekly";

    /// <summary>
    /// Donut/Line chart bileşenleri bu event'i dinleyerek kendi UpdateSeriesAsync()
    /// çağrılarını tetikler. DashboardBase, API yanıtı geldikten sonra bu event'i fırlatır.
    /// </summary>
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

    /// <summary>
    /// Singleton <see cref="GlobalTimerService"/> içindeki high-water mark;
    /// sayfa dispose olsa bile korunur.
    /// </summary>
    protected int DisplayTodayStudiedMinutes => GlobalTimerService.DisplayTodayStudiedMinutes;

    protected string GetTodayStudyLabel()
    {
        var timerSeconds = GlobalTimerService.DisplayTodayStudiedTotalSeconds;
        if (timerSeconds > 0)
            return GlobalTimerService.DisplayTodayStudiedLabel;

        if (summary != null && summary.TodayStudiedMinutes > 0)
            return StudyDurationMetrics.FormatDisplay(TimeSpan.FromMinutes(summary.TodayStudiedMinutes));

        return "0dk";
    }

    // ── Event Handlers ─────────────────────────────────────────────────────────

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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dashboard soft reload failed: {ex.Message}"); }
        finally { StateHasChanged(); }
    }

    private async Task SoftReloadAndRefreshCharts()
    {
        try
        {
            var newSummary = await DashboardService.GetSummaryAsync();
            if (newSummary != null) ApplySummary(newSummary);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dashboard chart reload failed: {ex.Message}"); }
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

    /// <summary>
    /// Önce SQLite snapshot → anında UI; ardından API arka planda (stale yanıt high-water'ı ezmez).
    /// </summary>
    protected async Task LoadAsync()
    {
        try
        {
            loadError = null;
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
        catch (Exception ex)
        {
            // F45: Sessizce yutma — ilk yüklemede her iki kaynak da başarısızsa kullanıcıya durum bırak.
            if (summary == null)
                loadError = "Pano yüklenemedi. Lütfen tekrar deneyin.";
            System.Diagnostics.Debug.WriteLine($"Dashboard load failed: {ex.Message}");
        }
        finally { isLoading = false; }
    }

    private void ApplySummary(DashboardSummaryDto newSummary)
    {
        GlobalTimerService.MergeApiSummaryMinutes(newSummary.TodayStudiedMinutes);

        var displayMinutes = GlobalTimerService.DisplayTodayStudiedMinutes;
        if (displayMinutes > newSummary.TodayStudiedMinutes)
        {
            var delta = displayMinutes - newSummary.TodayStudiedMinutes;
            newSummary.TodayStudiedMinutes = displayMinutes;
            PatchTodayChartBuckets(newSummary, delta);
        }
        else if (GlobalTimerService.DisplayTodayStudiedTotalSeconds > 0
                 && newSummary.TodayStudiedMinutes == 0)
        {
            newSummary.TodayStudiedMinutes = displayMinutes;
        }

        summary = newSummary;
    }

    /// <summary>
    /// KPI high-water API'den ilerideyken bugünkü grafik çubuklarına eksik dakikayı ekler.
    /// </summary>
    private static void PatchTodayChartBuckets(DashboardSummaryDto summary, int deltaMinutes)
    {
        if (deltaMinutes <= 0) return;

        var hour = DateTime.Now.Hour;
        var hourLabel = $"{hour:D2}:00";
        var daily = summary.DailyChartData.FirstOrDefault(c => c.Label == hourLabel);
        if (daily != null)
            daily.Value += deltaMinutes;
        else
            summary.DailyChartData.Add(new ChartDataDto { Label = hourLabel, Value = deltaMinutes });

        if (summary.WeeklyChartData.Count > 0)
        {
            var todaySuffix = $" {DateTime.Today.Day}";
            var weekly = summary.WeeklyChartData.FirstOrDefault(c => c.Label.EndsWith(todaySuffix, StringComparison.Ordinal));
            if (weekly != null)
                weekly.Value += deltaMinutes;
        }
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
