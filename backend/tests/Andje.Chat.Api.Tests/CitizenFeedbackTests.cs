using System.Net;
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

/// <summary>
/// Pruebas del endpoint publico de encuesta contra PostgreSQL real.
/// </summary>
public class CitizenFeedbackTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly AgentActor Agent =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Funcionario Demo");

    [SkippableFact]
    public async Task Feedback_valido_en_conversacion_cerrada_se_persiste_sin_comentario_en_auditoria()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedClosedConversationAsync("Ciudadano Demo Widget", "Seguimiento");

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        const string comment = "Atencion clara y oportuna del funcionario";

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(5, comment));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ConversationFeedbackDto>();
        Assert.NotNull(dto);
        Assert.Equal(5, dto.Rating);

        await using var db = new ChatDbContext(fixture.Options);
        var feedback = await db.ConversationFeedback.SingleAsync(f => f.ConversationId == conversationId);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal(comment, feedback.Comment);

        var audit = await db.AuditEvents.SingleAsync(
            a => a.ConversationId == conversationId && a.EventType == "conversation.feedback_submitted");
        Assert.Contains(feedback.Id.ToString(), audit.DataJson);
        Assert.Contains("rating", audit.DataJson);
        Assert.DoesNotContain("Atencion clara", audit.DataJson);
    }

    [SkippableFact]
    public async Task Feedback_en_conversacion_no_cerrada_es_rechazado()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        Guid conversationId;
        await using (var seedDb = new ChatDbContext(fixture.Options))
        {
            var store = new PostgresConversationStore(seedDb);
            var dto = await store.StartConversationAsync(
                "Ciudadano Abierto", "Tramite", "demo-v1", DateTimeOffset.UtcNow);
            conversationId = dto.Id;
        }

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(4, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_fuera_de_rango_es_rechazado(int rating)
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedClosedConversationAsync("Ciudadano Rango", null);

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(rating, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Comentario_demasiado_largo_es_rechazado()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedClosedConversationAsync("Ciudadano Largo", null);

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(5, new string('a', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Segundo_feedback_para_la_misma_conversacion_es_rechazado()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedClosedConversationAsync("Ciudadano Doble", null);

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback", new SubmitFeedbackRequest(5, "Primero"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback", new SubmitFeedbackRequest(3, "Segundo"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [SkippableFact]
    public async Task El_feedback_no_aparece_como_mensaje_en_el_historial()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedClosedConversationAsync("Ciudadano Historial", null, includeMessage: true);

        await using var factory = new CitizenFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(5, "Comentario que no debe verse en el historial"));

        await using var db = new ChatDbContext(fixture.Options);
        var history = await new PostgresConversationStore(db).GetMessagesAsync(conversationId);
        Assert.NotNull(history);
        var message = Assert.Single(history);
        Assert.Equal("Consulta demo widget", message.Content);
        Assert.DoesNotContain(history, m => m.Content.Contains("Comentario que no debe verse"));
    }

    private async Task<Guid> SeedClosedConversationAsync(
        string? displayName, string? topic, bool includeMessage = false)
    {
        await using var db = new ChatDbContext(fixture.Options);
        var store = new PostgresConversationStore(db);
        var dto = await store.StartConversationAsync(displayName, topic, "demo-v1", DateTimeOffset.UtcNow);
        if (includeMessage)
        {
            await store.AppendMessageAsync(dto.Id, SenderType.Visitor, "Consulta demo widget");
        }

        await store.CloseConversationAsync(dto.Id, Agent);
        return dto.Id;
    }

    private sealed class CitizenFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("AgentAccess:DevelopmentAccessCode", "test-agent-code");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ChatDbContext>>();
                services.AddDbContext<ChatDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }
}
