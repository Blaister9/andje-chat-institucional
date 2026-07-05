using Andje.Chat.Api.Hubs;

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

var app = builder.Build();

app.UseCors(corsPolicy);

app.MapHealthChecks("/health");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

// Expone Program a las pruebas de integración (WebApplicationFactory).
public partial class Program;
