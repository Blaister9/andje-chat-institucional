using System.Threading.RateLimiting;
using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Hubs;
using Andje.Chat.Api.Security;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "frontends-locales";
const string agentSessionRateLimit = "agent-session";

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        // Lista explicita por configuracion; sin comodines porque se permiten
        // credenciales (token de agente por query en SignalR).
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Limita el cuerpo de las peticiones HTTP muy por debajo del maximo por
// defecto de Kestrel (~28 MiB), con holgura para SignalR long polling.
var maxRequestBodyBytes = builder.Configuration.GetValue("Security:MaxRequestBodyBytes", 1_048_576L);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

builder.Services.AddHealthChecks();
builder.Services.AddSignalR(options =>
{
    // El hub valida 2000 caracteres por mensaje; 32 KiB deja holgura para el
    // sobre del protocolo sin permitir payloads abusivos.
    options.MaximumReceiveMessageSize = builder.Configuration.GetValue(
        "Security:SignalRMaxMessageBytes", 32 * 1024L);
});

var agentSessionPermitLimit = builder.Configuration.GetValue("RateLimiting:AgentSessionPermitLimit", 10);
var agentSessionWindowSeconds = builder.Configuration.GetValue("RateLimiting:AgentSessionWindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(agentSessionRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Nota: la IP proviene de la conexion directa. Detras de un proxy
            // reverso habria que honrar X-Forwarded-For (pendiente de despliegue).
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, agentSessionPermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, agentSessionWindowSeconds)),
                QueueLimit = 0,
            }));
});

ApplyAgentAccessEnvironmentAliases(builder.Configuration);
builder.Services.Configure<AgentAccessOptions>(
    builder.Configuration.GetSection("AgentAccess"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAgentSessionService, AgentSessionService>();

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ChatDb"),
        npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddScoped<IConversationStore, PostgresConversationStore>();

var app = builder.Build();

// Falla rapido ante configuracion insegura fuera de desarrollo; en desarrollo
// solo advierte para no romper el flujo local.
var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", true);
var startupIssues = SecurityStartupValidation.Collect(
    app.Environment.IsDevelopment(),
    app.Configuration.GetSection("AgentAccess").Get<AgentAccessOptions>() ?? new AgentAccessOptions(),
    corsOrigins,
    autoMigrate,
    !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("ChatDb")));

if (startupIssues.Count > 0)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Security");
    if (app.Environment.IsDevelopment())
    {
        foreach (var issue in startupIssues)
        {
            logger.LogWarning("Configuracion insegura (solo advertencia en desarrollo): {Issue}", issue);
        }
    }
    else
    {
        foreach (var issue in startupIssues)
        {
            logger.LogError("Configuracion insegura bloqueante: {Issue}", issue);
        }

        throw new InvalidOperationException(
            "La configuracion tiene problemas de seguridad bloqueantes: " +
            string.Join(" | ", startupIssues));
    }
}

// Aplica las migraciones pendientes al arrancar (reproducible en docker
// compose). Las pruebas lo desactivan con Database:AutoMigrate=false.
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ChatDbContext>().Database.MigrateAsync();
}

app.UseSecurityHeaders();

// HSTS solo tiene efecto sobre respuestas HTTPS; en el desarrollo local (HTTP)
// no aplica. Se deja preparado para entornos con TLS real.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors(corsPolicy);
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapPost("/api/agent-sessions", (
    CreateAgentSessionRequest request,
    IAgentSessionService sessions) =>
{
    try
    {
        return Results.Ok(sessions.CreateSession(request));
    }
    catch (AgentSessionRejectedException)
    {
        return Results.Unauthorized();
    }
})
.RequireRateLimiting(agentSessionRateLimit);
app.MapHub<ChatHub>("/hubs/chat");

static void ApplyAgentAccessEnvironmentAliases(IConfiguration configuration)
{
    var enabled = Environment.GetEnvironmentVariable("ANDJE_AGENT_ACCESS_ENABLED");
    if (!string.IsNullOrWhiteSpace(enabled))
    {
        configuration["AgentAccess:Enabled"] = enabled;
    }

    var devCode = Environment.GetEnvironmentVariable("ANDJE_AGENT_DEV_CODE");
    if (!string.IsNullOrWhiteSpace(devCode))
    {
        configuration["AgentAccess:DevelopmentAccessCode"] = devCode;
    }

    var sessionMinutes = Environment.GetEnvironmentVariable("ANDJE_AGENT_SESSION_MINUTES");
    if (!string.IsNullOrWhiteSpace(sessionMinutes))
    {
        configuration["AgentAccess:SessionMinutes"] = sessionMinutes;
    }
}

app.Run();

// Expone Program a las pruebas de integración (WebApplicationFactory).
public partial class Program;
