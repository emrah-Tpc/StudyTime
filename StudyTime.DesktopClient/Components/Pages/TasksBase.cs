using Microsoft.AspNetCore.Components;
using StudyTime.Application.DTOs.Tasks;
using StudyTime.Application.DTOs.Lessons;
using StudyTime.Domain.Enums;
using StudyTime.DesktopClient.Offline;
using System.Globalization;

namespace StudyTime.DesktopClient.Components.Pages;

public abstract class TasksBase : ComponentBase
{
    [Inject] protected SyncedTaskApiService TaskService { get; set; } = default!;
    [Inject] protected SyncedLessonApiService LessonService { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    
    protected DateTime CurrentDate = DateTime.Today;
    protected CultureInfo CurrentCulture = new CultureInfo("tr-TR");
    
    protected List<CalendarDay> CalendarDays = new();
    protected bool isLoading = true;
    protected string? loadError;
    protected CalendarDay? selectedDay;
    protected TaskDto? selectedDetailTask;

    // ── Görev oluşturma (Görevler sayfasından doğrudan ekleme) ──────────────
    protected List<LessonListItemDto> Lessons = new();
    protected bool showCreateTask;
    protected bool isSavingTask;
    protected string? createError;
    protected CreateTaskDto newTask = new();

    // <select> Guid? binding'i icin guvenli string proxy.
    protected string? SelectedLessonId
    {
        get => newTask.LessonId?.ToString();
        set => newTask.LessonId = Guid.TryParse(value, out var g) ? g : (Guid?)null;
    }

    protected string CurrentMonthName => CurrentDate.ToString("MMMM", CurrentCulture);
    protected int CurrentYear => CurrentDate.Year;

    protected override async Task OnInitializedAsync()
    {
        await LoadCalendar();
        await LoadLessons();
    }

    protected async Task LoadLessons()
    {
        try
        {
            var all = await LessonService.GetAllAsync();
            // Yeni görev yalnız aktif alanlara atanabilsin (arşivlenmişi gizle).
            Lessons = all.Where(l => l.Status == LessonStatus.Active).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadLessons failed: {ex.Message}");
        }
    }

    protected void OpenCreateTask(DateTime? date = null)
    {
        var start = (date ?? DateTime.Today).Date.AddHours(9);
        newTask = new CreateTaskDto
        {
            Title = "",
            StartDate = start,
            LessonId = Lessons.FirstOrDefault()?.Id,
            Status = StudyTime.Domain.Enums.TaskStatus.Pending
        };
        createError = null;
        showCreateTask = true;
    }

    protected void CloseCreateTask()
    {
        showCreateTask = false;
        createError = null;
    }

    protected async Task SaveNewTask()
    {
        if (isSavingTask) return;

        if (string.IsNullOrWhiteSpace(newTask.Title))
        {
            createError = "Lütfen görev adı girin.";
            return;
        }
        if (newTask.LessonId == null || newTask.LessonId == Guid.Empty)
        {
            createError = "Lütfen bir alan (ders) seçin.";
            return;
        }

        isSavingTask = true;
        createError = null;
        try
        {
            newTask.Title = newTask.Title.Trim();
            await TaskService.CreateAsync(newTask);
            showCreateTask = false;
            await LoadCalendar();

            // Eklenen görevin gününü açık tut ki kullanıcı sonucu görsün.
            if (newTask.StartDate.HasValue)
                selectedDay = CalendarDays.FirstOrDefault(d => d.Date.Date == newTask.StartDate.Value.Date);
        }
        catch (Exception ex)
        {
            createError = "Görev eklenemedi. Lütfen tekrar deneyin.";
            System.Diagnostics.Debug.WriteLine($"SaveNewTask failed: {ex.Message}");
        }
        finally
        {
            isSavingTask = false;
        }
    }

    protected async Task LoadCalendar()
    {
        isLoading = true;
        loadError = null;
        CalendarDays.Clear();

        var firstDayOfMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        var startGridDate = firstDayOfMonth;
        while (startGridDate.DayOfWeek != DayOfWeek.Monday)
        {
            startGridDate = startGridDate.AddDays(-1);
        }

        var endGridDate = startGridDate.AddDays(34);
        while (endGridDate < lastDayOfMonth || endGridDate.DayOfWeek != DayOfWeek.Sunday)
        {
             endGridDate = endGridDate.AddDays(1);
        }

        try
        {
            var allTasks = await TaskService.GetTasksByDateRangeAsync(startGridDate, endGridDate);

            for (var date = startGridDate; date <= endGridDate; date = date.AddDays(1))
            {
                var dayTasks = allTasks.Where(t => t.StartDate.HasValue && t.StartDate.Value.Date == date.Date).ToList();

                CalendarDays.Add(new CalendarDay
                {
                    Date = date,
                    IsCurrentMonth = date.Month == CurrentDate.Month,
                    Tasks = dayTasks
                });
            }
        }
        catch (Exception ex)
        {
            // F13: Hata yutulmasın/spinner takılmasın; kullanıcıya bildirilebilir bir durum bırak.
            loadError = "Takvim yüklenemedi. Lütfen tekrar deneyin.";
            System.Diagnostics.Debug.WriteLine($"LoadCalendar failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    protected async Task PreviousMonth()
    {
        CurrentDate = CurrentDate.AddMonths(-1);
        await LoadCalendar();
    }

    protected async Task NextMonth()
    {
        CurrentDate = CurrentDate.AddMonths(1);
        await LoadCalendar();
    }
    
    protected async Task GoToToday()
    {
        CurrentDate = DateTime.Today;
        await LoadCalendar();
    }

    protected bool IsToday(DateTime date) => date.Date == DateTime.Today;

    protected string GetHeatmapColor(int taskCount)
    {
        if (taskCount == 0) return "var(--bg-card)";
        
        var alpha = Math.Min(0.1 + (taskCount * 0.15), 0.6);
        return $"rgba(59, 130, 246, {alpha.ToString(CultureInfo.InvariantCulture)})"; 
    }

    protected void SelectDay(CalendarDay day)
    {
        selectedDay = day;
    }

    protected void CloseModal() => selectedDay = null;

    protected async Task ToggleTaskStatus(TaskDto task, object? checkedValue)
    {
        bool isChecked = (bool)(checkedValue ?? false);
        var newStatus = isChecked ? StudyTime.Domain.Enums.TaskStatus.Completed : StudyTime.Domain.Enums.TaskStatus.Pending;

        // Tamamlanan gorev geri alinabilir: backend artik idempotent reopen destekliyor.
        // (Eskiden buradaki guard, tikten cikarmayi sessizce engelliyordu.)
        task.Status = newStatus;
        
        try 
        {
            await TaskService.UpdateTaskStatusAsync(task.Id, newStatus);
        }
        catch 
        {
             task.Status = isChecked ? StudyTime.Domain.Enums.TaskStatus.Pending : StudyTime.Domain.Enums.TaskStatus.Completed;
        }
    }
    
    protected bool IsTaskCompleted(TaskDto task) => task.Status == StudyTime.Domain.Enums.TaskStatus.Completed;

    protected string GetFormattedDuration(TimeSpan ts)
    {
        int totalMinutes = (int)ts.TotalMinutes;
        if (totalMinutes < 60) return $"{totalMinutes}dk";
        
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        return $"{hours}s {minutes}dk";
    }

    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public List<TaskDto> Tasks { get; set; } = new();
    }
}
