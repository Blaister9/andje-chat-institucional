using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andje.Chat.Api.Tests;

public class ConsoleEndpointsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task Endpoints_de_consola_requieren_token()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var factory = new ConsoleEndpointFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/console/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Token_valido_permita_summary_catalogo_y_etiquetas()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var factory = new ConsoleEndpointFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);

        var summary = await client.GetFromJsonAsync<ConsoleSummaryDto>("/api/console/summary");
        var responses = await client.GetFromJsonAsync<List<CannedResponseDto>>("/api/console/canned-responses");
        var tags = await client.GetFromJsonAsync<List<ConversationTagDto>>("/api/console/tags");

        Assert.NotNull(summary);
        Assert.NotNull(responses);
        Assert.NotNull(tags);
        Assert.Contains(responses, r => r.Title == "Saludo institucional" && r.IsActive);
        Assert.Contains(tags, t => t.Name == "Seguimiento");
    }

    [SkippableFact]
    public async Task Gestion_de_respuestas_rapidas_audita_sin_guardar_cuerpo_en_auditoria()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var factory = new ConsoleEndpointFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);

        const string responseBody = "Hola, con gusto te orientamos desde una prueba de consola.";
        var createdResponse = await client.PostAsJsonAsync(
            "/api/console/canned-responses",
            new UpsertCannedResponseRequest("Saludo demo QA", responseBody, 5, true));
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<CannedResponseDto>();
        Assert.NotNull(created);

        var deactivatedResponse = await client.PatchAsync(
            $"/api/console/canned-responses/{created.Id}/deactivate",
            content: null);
        deactivatedResponse.EnsureSuccessStatusCode();

        await using var db = new ChatDbContext(fixture.Options);
        var audit = await db.AuditEvents
            .Where(a => a.EventType == "canned_response.created" ||
                        a.EventType == "canned_response.deactivated")
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(4)
            .ToListAsync();

        Assert.Contains(audit, a => a.DataJson != null && a.DataJson.Contains(created.Id.ToString()));
        Assert.DoesNotContain(audit, a => a.DataJson != null && a.DataJson.Contains(responseBody));
    }

    [SkippableFact]
    public async Task Notas_y_etiquetas_son_internas_y_no_aparecen_en_historial_del_widget()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        Guid conversationId;
        await using (var seedDb = new ChatDbContext(fixture.Options))
        {
            var store = new PostgresConversationStore(seedDb);
            var conversation = await store.StartConversationAsync("Ciudadano QA UX");
            conversationId = conversation.Id;
            await store.AppendMessageAsync(conversationId, SenderType.Visitor, "Consulta visible del ciudadano");
        }

        await using var factory = new ConsoleEndpointFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);
        const string internalNoteBody = "Nota interna reservada que no debe ver el widget";

        var noteResponse = await client.PostAsJsonAsync(
            $"/api/console/conversations/{conversationId}/notes",
            new CreateInternalNoteRequest(internalNoteBody));
        noteResponse.EnsureSuccessStatusCode();

        var tags = await client.GetFromJsonAsync<List<ConversationTagDto>>("/api/console/tags");
        var tag = Assert.Single(tags!, t => t.Name == "Seguimiento");
        var tagResponse = await client.PostAsync(
            $"/api/console/conversations/{conversationId}/tags/{tag.Id}",
            content: null);
        tagResponse.EnsureSuccessStatusCode();

        var conversations = await client.GetFromJsonAsync<List<ConsoleConversationDto>>("/api/console/conversations");
        var conversationDto = Assert.Single(conversations!, c => c.Id == conversationId);
        Assert.Contains(conversationDto.Tags, t => t.Name == "Seguimiento");
        Assert.Contains("Consulta visible", conversationDto.LastMessagePreview);

        await using var verifyDb = new ChatDbContext(fixture.Options);
        var history = await new PostgresConversationStore(verifyDb).GetMessagesAsync(conversationId);
        Assert.NotNull(history);
        var message = Assert.Single(history);
        Assert.Equal("Consulta visible del ciudadano", message.Content);
        Assert.DoesNotContain(internalNoteBody, message.Content);

        var auditEvents = await verifyDb.AuditEvents
            .Where(a => a.ConversationId == conversationId)
            .ToListAsync();
        Assert.DoesNotContain(auditEvents, audit => audit.DataJson != null && audit.DataJson.Contains(internalNoteBody));
        Assert.Contains(auditEvents, audit => audit.EventType == "internal_note.created");
    }

    private static HttpClient CreateAuthorizedClient(ConsoleEndpointFactory factory)
    {
        var client = factory.CreateClient();
        var sessions = factory.Services.GetRequiredService<IAgentSessionService>();
        var session = sessions.CreateSession(
            new CreateAgentSessionRequest("Agente QA Consola", "test-agent-code"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private sealed class ConsoleEndpointFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("AgentAccess:DevelopmentAccessCode", "test-agent-code");
            builder.UseSetting("AgentAccess:SessionMinutes", "120");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ChatDbContext>>();
                services.AddDbContext<ChatDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }
}
