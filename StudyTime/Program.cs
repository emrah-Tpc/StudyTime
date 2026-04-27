using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyTime.Application.Interfaces;
using StudyTime.Application.Services;
using StudyTime.Application.Validators.Tasks;
using StudyTime.Domain.Entities;
using StudyTime.Domain.Services;
using StudyTime.Infrastructure.Persistence;
using StudyTime.Infrastructure.Repositories;
using StudyTime.Infrastructure.Services;
using StudyTime.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Controllers ----------------
builder.Services.AddScoped<StudyTime.Filters.ActiveSessionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<StudyTime.Filters.ActiveSessionFilter>();
    
    // Global Authorization Policy (Secure by default)
    var policy = new AuthorizationPolicyBuilder()
                     .RequireAuthenticatedUser()
                     .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
})
.AddJsonOptions(options =>
{
    // Circular reference'ı (Session->Lesson->Sessions...) kır
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.WriteIndented = false;
});

// ---------------- CORS ----------------
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

    options.AddPolicy("ProductionCors",
        builder => builder
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

// ---------------- FluentValidation (NEW WAY) ----------------

builder.Services.AddValidatorsFromAssemblyContaining<UpdateTaskDtoValidator>();

// ---------------- DbContext ----------------
builder.Services.AddDbContext<StudyTimeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------- Identity & Auth ----------------
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<StudyTimeDbContext>()
.AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = builder.Configuration["JwtSettings:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrEmpty(secret))
{
    if (builder.Environment.IsDevelopment())
    {
        secret = "DevelopmentSuperSecretKeyWhichNeedsToBeAtLeast32BytesLong!";
    }
    else
    {
        throw new InvalidOperationException("JWT Secret is not configured. Please set 'JwtSettings:Secret' or 'JWT_SECRET' environment variable in Production.");
    }
}

var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISystemContextState, SystemContextState>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---------------- Application Services ----------------
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<StudySessionService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// ---------------- Domain Services ----------------
builder.Services.AddSingleton<ProductivityCalculator>();

// ---------------- Repositories ----------------
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IStudySessionRepository, StudySessionRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<DevelopmentMssqlSeeder>();

// ---------------- Swagger ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- HTTP pipeline ----------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentMssqlSeeder>();
    await seeder.SeedAsync();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("ProductionCors");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();



app.Run();

