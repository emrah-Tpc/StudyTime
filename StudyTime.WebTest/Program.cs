using StudyTime.WebTest.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<StudyTime.DesktopClient.Services.SyncedDashboardApiService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.SyncedTaskApiService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.SyncedLessonApiService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.SyncedStatisticsApiService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.StudySessionApiService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.ThemeService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.AuthService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.ConnectivityService>();
builder.Services.AddSingleton<StudyTime.DesktopClient.Services.GlobalTimerService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Offline.SyncStatusService>();
builder.Services.AddScoped<StudyTime.DesktopClient.Services.IPlatformDetector, StudyTime.DesktopClient.Services.MockPlatformDetector>();
builder.Services.AddSingleton<StudyTime.DesktopClient.StudyTimeAppOptions>();
builder.Services.AddScoped<StudyTime.DesktopClient.AppNotificationCenterService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
