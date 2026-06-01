using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OracleCMS.CarStock.API.Data;
using OracleCMS.CarStock.API.Middleware;
using OracleCMS.CarStock.API.Repositories;
using OracleCMS.CarStock.API.Repositories.Interfaces;
using OracleCMS.CarStock.API.Services;
using OracleCMS.CarStock.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("Missing Jwt:Secret.");
var jwtIssuer = jwtSection["Issuer"]
    ?? throw new InvalidOperationException("Missing Jwt:Issuer.");
var jwtAudience = jwtSection["Audience"]
    ?? throw new InvalidOperationException("Missing Jwt:Audience.");

if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes.");
}

if (jwtSecret == "REPLACE_WITH_32_CHAR_MIN_SECRET_KEY_HERE")
{
    throw new InvalidOperationException(
        "Jwt:Secret is still the placeholder. Set a real secret via appsettings.Development.json, user-secrets, or environment variables.");
}

builder.Services.AddSingleton<ISqliteConnectionFactory>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");
    return new SqliteConnectionFactory(cs);
});

builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddScoped<IDealerRepository, DealerRepository>();
builder.Services.AddScoped<ICarRepository, CarRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICarService, CarService>();

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.EnableJWTBearerAuth = true;
    o.DocumentSettings = s =>
    {
        s.Title = "OracleCMS Car Stock API";
        s.Version = "v1";
        s.Description = "Multi-tenant car stock management API for dealers.";
    };
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new
        {
            error = "Too many requests",
            detail = "Rate limit exceeded. Slow down and try again shortly."
        });
        await context.HttpContext.Response.WriteAsync(payload, cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<SqliteHealthCheck>("sqlite", tags: new[] { "ready" });

var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");
DatabaseInitializer.Initialize(connectionString);

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Preserve the existing { error, detail } error envelope so integration tests
// and clients aren't broken by switching to FastEndpoints' default response shape.
app.UseFastEndpoints(c =>
{
    c.Errors.ResponseBuilder = (failures, _, _) =>
    {
        var detail = string.Join(" ", failures
            .Select(f => string.IsNullOrWhiteSpace(f.ErrorMessage)
                ? $"{f.PropertyName} is invalid."
                : f.ErrorMessage));

        return new
        {
            error = "Validation failed",
            detail = string.IsNullOrWhiteSpace(detail) ? "One or more fields are invalid." : detail
        };
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            }),
            durationMs = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(payload);
    }
});

app.Run();

/// <summary>
/// Marker partial used by integration tests to discover the host via WebApplicationFactory.
/// </summary>
public partial class Program;
