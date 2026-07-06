using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andje.Chat.Api.Tests;

/// <summary>
/// Fábrica para las pruebas del flujo realtime: sustituye el store de
/// PostgreSQL por el de memoria y desactiva la migración automática, de modo
/// que las pruebas del hub no dependan de una base de datos.
/// La persistencia real se prueba aparte en PostgresConversationStoreTests.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("AgentAccess:DevelopmentAccessCode", "test-agent-code");
        builder.UseSetting("AgentAccess:SessionMinutes", "120");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConversationStore>();
            services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        });
    }
}
