using System.Net;
using System.Net.Http.Json;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andje.Chat.Api.Tests;

public class SecurityHardeningTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Health_incluye_headers_de_seguridad()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("nosniff", Value(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Value(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Value(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Value(response, "Permissions-Policy"));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:5174")]
    public async Task Cors_permite_los_origenes_locales_de_consola_y_widget(string origin)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);

        var response = await client.SendAsync(request);

        Assert.Equal(origin, Value(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_no_permite_un_origen_no_autorizado()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://malicioso.example");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Agent_sessions_rechaza_codigo_vacio()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/agent-sessions", new { displayName = "Agente QA", accessCode = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_sessions_rechaza_codigo_demasiado_largo_sin_error_de_servidor()
    {
        var client = factory.CreateClient();
        var codigoEnorme = new string('a', 5000);

        var response = await client.PostAsJsonAsync(
            "/api/agent-sessions", new { displayName = "Agente QA", accessCode = codigoEnorme });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_sessions_aplica_rate_limiting()
    {
        await using var limitedFactory = new RateLimitedTestWebApplicationFactory();
        var client = limitedFactory.CreateClient();

        // Con el limite en 2 por ventana, la tercera peticion debe rechazarse.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/agent-sessions", new { displayName = "Agente QA", accessCode = "incorrecto" });
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    private static string? Value(HttpResponseMessage response, string header)
    {
        if (response.Headers.TryGetValues(header, out var values))
        {
            return string.Join(",", values);
        }

        if (response.Content.Headers.TryGetValues(header, out var contentValues))
        {
            return string.Join(",", contentValues);
        }

        return null;
    }

    private sealed class RateLimitedTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("AgentAccess:DevelopmentAccessCode", "test-agent-code");
            builder.UseSetting("RateLimiting:AgentSessionPermitLimit", "2");
            builder.UseSetting("RateLimiting:AgentSessionWindowSeconds", "60");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConversationStore>();
                services.AddSingleton<IConversationStore, InMemoryConversationStore>();
            });
        }
    }
}
