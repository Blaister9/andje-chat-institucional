using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Andje.Chat.Api.Data;

/// <summary>
/// Usada únicamente por la CLI de EF (dotnet ef migrations …); apunta al
/// PostgreSQL local de docker-compose. No se usa en tiempo de ejecución.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("ANDJE_DB_PASSWORD") ?? "andje_dev_local";
        var port = Environment.GetEnvironmentVariable("ANDJE_DB_PORT") ?? "5433";
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql($"Host=localhost;Port={port};Database=andje_chat;Username=andje;Password={password}")
            .Options;
        return new ChatDbContext(options);
    }
}
