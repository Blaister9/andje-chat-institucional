using System.Threading.RateLimiting;
using Andje.Chat.Api.ConsoleApi;
using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Diagnostics;
using Andje.Chat.Api.Hubs;
using Andje.Chat.Api.PublicApi;
using Andje.Chat.Api.Security;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "frontends-locales";
const string agentSessionRateLimit = "agent-session";

var corsOrigins = GetCorsOrigins(builder.Configuration);
var forwardedHeadersOptions = ForwardedHeadersConfiguration.GetForwardedHeadersOptions(builder.Configuration);
ApplyDiagnosticsEnvironmentAliases(builder.Configuration);
ApplyHttpsEnvironmentAliases(builder.Configuration);

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
builder.Services.Configure<DiagnosticsOptions>(
    builder.Configuration.GetSection("Diagnostics"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAgentSessionService, AgentSessionService>();

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ChatDb"),
        npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddScoped<IConversationStore, PostgresConversationStore>();
builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();

var app = builder.Build();

// Falla rapido ante configuracion insegura fuera de desarrollo/pruebas; en
// desarrollo y pruebas solo advierte para no romper el flujo local ni el CI.
var isDevelopmentOrTest = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test");
var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", true);
var httpsOptions = app.Configuration.GetSection("Https").Get<AndjeHttpsOptions>() ?? new AndjeHttpsOptions();
var startupIssues = SecurityStartupValidation.Collect(
    isDevelopmentOrTest,
    app.Configuration.GetSection("AgentAccess").Get<AgentAccessOptions>() ?? new AgentAccessOptions(),
    corsOrigins,
    autoMigrate,
    !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("ChatDb")),
    forwardedHeadersOptions.Enabled,
    ForwardedHeadersConfiguration.HasKnownProxyOrNetwork(forwardedHeadersOptions),
    httpsOptions.RequireHttps,
    httpsOptions.UseHsts);

if (startupIssues.Count > 0)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Security");
    if (isDevelopmentOrTest)
    {
        foreach (var issue in startupIssues)
        {
            logger.LogWarning("Configuracion insegura (solo advertencia en desarrollo/pruebas): {Issue}", issue);
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

if (forwardedHeadersOptions.Enabled)
{
    app.UseForwardedHeaders(ForwardedHeadersConfiguration.ToMiddlewareOptions(forwardedHeadersOptions));
}

app.UseRequestCorrelation();
app.UseSecurityHeaders();

if (httpsOptions.RequireHttps)
{
    app.UseHttpsRedirection();
}

// HSTS solo se activa explicitamente y fuera de desarrollo/pruebas. En demos
// locales HTTP queda apagado para no romper localhost/LAN.
if (httpsOptions.UseHsts && !isDevelopmentOrTest)
{
    app.UseHsts();
}

app.UseCors(corsPolicy);
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapGet("/api/diagnostics/status", async (
    IDiagnosticsService diagnostics,
    CancellationToken cancellationToken) =>
{
    var options = app.Configuration.GetSection("Diagnostics").Get<DiagnosticsOptions>()
        ?? new DiagnosticsOptions();
    var allowed = app.Environment.IsDevelopment() ||
                  app.Environment.IsEnvironment("Test") ||
                  options.Enabled;
    if (!allowed)
    {
        return Results.NotFound();
    }

    var status = await diagnostics.GetStatusAsync(
        app.Environment.EnvironmentName,
        options.IncludeCounts,
        cancellationToken);
    return Results.Ok(status);
});
app.MapConsoleEndpoints();
app.MapCitizenEndpoints();

if (app.Environment.IsEnvironment("Test"))
{
    app.MapGet("/__test/request-scheme", (HttpContext context) =>
        Results.Json(new
        {
            scheme = context.Request.Scheme,
            host = context.Request.Host.Value,
            remoteIp = context.Connection.RemoteIpAddress?.ToString(),
        }));
}

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

static string[] GetCorsOrigins(IConfiguration configuration)
{
    var configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var demoExtraOrigins = Environment.GetEnvironmentVariable("DEMO_ALLOWED_ORIGINS");
    if (string.IsNullOrWhiteSpace(demoExtraOrigins))
    {
        return configured;
    }

    var extra = demoExtraOrigins
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return configured
        .Concat(extra)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static void ApplyHttpsEnvironmentAliases(IConfiguration configuration)
{
    var requireHttps = Environment.GetEnvironmentVariable("ANDJE_HTTPS_REQUIRE");
    if (!string.IsNullOrWhiteSpace(requireHttps))
    {
        configuration["Https:RequireHttps"] = requireHttps;
    }

    var useHsts = Environment.GetEnvironmentVariable("ANDJE_HTTPS_HSTS");
    if (!string.IsNullOrWhiteSpace(useHsts))
    {
        configuration["Https:UseHsts"] = useHsts;
    }
}

static void ApplyDiagnosticsEnvironmentAliases(IConfiguration configuration)
{
    var enabled = Environment.GetEnvironmentVariable("ANDJE_DIAGNOSTICS_ENABLED");
    if (!string.IsNullOrWhiteSpace(enabled))
    {
        configuration["Diagnostics:Enabled"] = enabled;
    }

    var includeCounts = Environment.GetEnvironmentVariable("ANDJE_DIAGNOSTICS_INCLUDE_COUNTS");
    if (!string.IsNullOrWhiteSpace(includeCounts))
    {
        configuration["Diagnostics:IncludeCounts"] = includeCounts;
    }
}

app.Run();

// Expone Program a las pruebas de integración (WebApplicationFactory).
public partial class Program;
