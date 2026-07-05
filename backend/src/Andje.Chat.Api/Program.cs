using Andje.Chat.Api.Data;
using Andje.Chat.Api.Hubs;
using Andje.Chat.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "frontends-locales";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ChatDb"),
        npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddScoped<IConversationStore, PostgresConversationStore>();

var app = builder.Build();

// Aplica las migraciones pendientes al arrancar (reproducible en docker
// compose). Las pruebas lo desactivan con Database:AutoMigrate=false.
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ChatDbContext>().Database.MigrateAsync();
}

app.UseCors(corsPolicy);

app.MapHealthChecks("/health");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

// Expone Program a las pruebas de integración (WebApplicationFactory).
public partial class Program;
