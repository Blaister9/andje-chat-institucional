using System.Net;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Diagnostics;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andje.Chat.Api.Tests;

public class ObservabilityTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Health_incluye_request_id()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Request-ID"));
    }

    [Fact]
    public async Task Request_id_entrante_se_reutiliza_en_respuesta()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Request-ID", "qa-request-id");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("qa-request-id", response.Headers.GetValues("X-Request-ID").Single());
    }

    [Fact]
    public async Task Diagnostico_responde_en_test_y_no_expone_secretos()
    {
        await using var diagnosticsFactory = new DiagnosticsTestWebApplicationFactory("Test", enabled: false);
        var client = diagnosticsFactory.CreateClient();

        var response = await client.GetAsync("/api/diagnostics/status");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", payload);
        Assert.Contains("\"environment\":\"Test\"", payload);
        Assert.Contains("\"counts\"", payload);
        Assert.DoesNotContain("andje_dev_local", payload);
        Assert.DoesNotContain("test-agent-code", payload);
        Assert.DoesNotContain("ConnectionStrings", payload);
        Assert.DoesNotContain("Password", payload);
    }

    [Fact]
    public async Task Diagnostico_no_se_expone_fuera_de_desarrollo_si_no_esta_habilitado()
    {
        await using var diagnosticsFactory = new DiagnosticsTestWebApplicationFactory("Production", enabled: false);
        var client = diagnosticsFactory.CreateClient();

        var response = await client.GetAsync("/api/diagnostics/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Diagnostico_se_expone_fuera_de_desarrollo_si_esta_habilitado()
    {
        await using var diagnosticsFactory = new DiagnosticsTestWebApplicationFactory("Production", enabled: true);
        var client = diagnosticsFactory.CreateClient();

        var response = await client.GetAsync("/api/diagnostics/status");

        response.EnsureSuccessStatusCode();
    }

    private sealed class DiagnosticsTestWebApplicationFactory(string environment, bool enabled)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("AgentAccess:DevelopmentAccessCode", "codigo-fuerte-diagnostico");
            builder.UseSetting("Diagnostics:Enabled", enabled.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConversationStore>();
                services.AddSingleton<IConversationStore, InMemoryConversationStore>();
                services.RemoveAll<IDiagnosticsService>();
                services.AddSingleton<IDiagnosticsService, FakeDiagnosticsService>();
            });
        }
    }

    private sealed class FakeDiagnosticsService : IDiagnosticsService
    {
        public Task<DiagnosticsStatus> GetStatusAsync(
            string environmentName,
            bool includeCounts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiagnosticsStatus(
                "Healthy",
                environmentName,
                "Reachable",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                "dev",
                includeCounts ? new OperationalCounts(1, 1, 0, 0, 0) : null));
    }
}

public class DiagnosticsServicePostgresTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly AgentActor Agent =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Agente Observabilidad");

    [SkippableFact]
    public async Task Conteos_operativos_reflejan_conversaciones_mensajes_y_auditoria()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var baselineDb = new ChatDbContext(fixture.Options);
        var baseline = await new DiagnosticsService(baselineDb, TimeProvider.System)
            .GetStatusAsync("Test", includeCounts: true);
        Assert.NotNull(baseline.Counts);

        Guid conversationId;
        await using (var db = new ChatDbContext(fixture.Options))
        {
            var store = new PostgresConversationStore(db);
            var conversation = await store.StartConversationAsync("Ciudadano QA");
            conversationId = conversation.Id;
            await store.AppendMessageAsync(conversationId, SenderType.Visitor, "Mensaje que no debe salir en diagnostico");
            await store.AppendMessageAsync(conversationId, SenderType.Agent, "Respuesta que no debe salir en diagnostico", Agent);
            await store.CloseConversationAsync(conversationId, Agent);
        }

        await using var verifyDb = new ChatDbContext(fixture.Options);
        var status = await new DiagnosticsService(verifyDb, TimeProvider.System)
            .GetStatusAsync("Test", includeCounts: true);

        Assert.Equal("Healthy", status.Status);
        Assert.Equal("Reachable", status.Database);
        Assert.NotNull(status.Counts);
        Assert.Equal(baseline.Counts.ConversationsTotal + 1, status.Counts.ConversationsTotal);
        Assert.Equal(baseline.Counts.ConversationsClosed + 1, status.Counts.ConversationsClosed);
        Assert.Equal(baseline.Counts.MessagesTotal + 2, status.Counts.MessagesTotal);
        Assert.Equal(baseline.Counts.AuditEventsTotal + 5, status.Counts.AuditEventsTotal);
    }
}
