using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
using StudyTime.Application.Validators.Tasks;
using StudyTime.Infrastructure.Persistence;
using StudyTime.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Controllers ----------------
builder.Services.AddControllers();

// ---------------- FluentValidation (NEW WAY) ----------------

builder.Services.AddValidatorsFromAssemblyContaining<UpdateTaskDtoValidator>();

// ---------------- DbContext ----------------
builder.Services.AddDbContext<StudyTimeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------- Application Services ----------------
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<StudySessionService>();

// ---------------- Repositories ----------------
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IStudySessionRepository, StudySessionRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();

// ---------------- Swagger ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- HTTP pipeline ----------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
