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
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Controllers ----------------
builder.Services.AddScoped<StudyTime.Filters.ActiveSessionFilter>();
builder.Services.AddScoped<StudyTime.Filters.FluentValidationActionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<StudyTime.Filters.ActiveSessionFilter>();
    // FluentValidation otomatik doğrulama (kayıtlı IValidator<T>'leri çalıştırır)
    options.Filters.Add<StudyTime.Filters.FluentValidationActionFilter>();

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

// JWT Settings — tek doğruluk kaynağı (hardcoded dev secret yalnızca BURADA).
const string DevJwtSecret = "DevelopmentSuperSecretKeyWhichNeedsToBeAtLeast32BytesLong!";
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSection["Secret"];
if (string.IsNullOrEmpty(secret))
    secret = Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrEmpty(secret))
{
    if (builder.Environment.IsDevelopment())
        secret = DevJwtSecret;
    else
        throw new InvalidOperationException("JWT Secret is not configured. Please set 'JwtSettings:Secret' or 'JWT_SECRET' environment variable in Production.");
}

var jwtSettings = new StudyTime.Application.Auth.JwtSettings
{
    Secret   = secret,
    Issuer   = jwtSection["Issuer"] ?? string.Empty,
    Audience = jwtSection["Audience"] ?? string.Empty,
    ExpiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"],
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture, out var expMin) ? expMin : 60
};
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenService>();

var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

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
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
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

// ---------------- Global Exception Handling (F18) ----------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<StudyTime.GlobalExceptionHandler>();

// ---------------- Rate Limiting (F19) ----------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // "auth" policy: brute-force'a açık uç noktalar için IP başına dakikada 10 istek.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ---------------- Swagger ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- HTTP pipeline ----------------
// Merkezi hata yönetimi en başta (tüm istisnaları JSON ProblemDetails'e indirger).
app.UseExceptionHandler();

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

app.UseRateLimiter();

app.MapControllers();



app.Run();

