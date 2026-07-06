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

/// <summary>
/// Dashboard de satisfaccion en la consola: metricas de summary y comentario
/// de feedback visible solo en endpoints internos con token de agente.
/// </summary>
public class FeedbackDashboardTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly AgentActor Agent =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Funcionario Demo");

    private const string Comment = "Muy clara la atencion del funcionario";

    [SkippableFact]
    public async Task Summary_sin_token_es_rechazado()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var factory = new ConsoleFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/console/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Summary_con_token_incluye_conteo_y_promedio_de_satisfaccion()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await SeedFeedbackAsync(rating: 5, comment: Comment);
        await SeedFeedbackAsync(rating: 3, comment: null);

        await using var factory = new ConsoleFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);

        var summary = await client.GetFromJsonAsync<ConsoleSummaryDto>("/api/console/summary");

        Assert.NotNull(summary);
        Assert.True(summary.FeedbackCount >= 2);
        Assert.NotNull(summary.AverageRating);
        Assert.InRange(summary.AverageRating!.Value, 1, 5);
        Assert.True(summary.PositiveFeedbackCount >= 1);
        Assert.NotNull(summary.PositiveFeedbackRate);
    }

    [SkippableFact]
    public async Task Conversacion_con_feedback_devuelve_rating_y_comentario_en_consola()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedFeedbackAsync(rating: 5, comment: Comment);

        await using var factory = new ConsoleFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);

        var conversations = await client.GetFromJsonAsync<List<ConsoleConversationDto>>("/api/console/conversations");
        var dto = Assert.Single(conversations!, c => c.Id == conversationId);

        Assert.Equal(5, dto.FeedbackRating);
        Assert.Equal(Comment, dto.FeedbackComment);
        Assert.NotNull(dto.FeedbackCreatedAtUtc);
    }

    [SkippableFact]
    public async Task Conversacion_cerrada_sin_encuesta_no_trae_feedback()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        Guid conversationId;
        await using (var db = new ChatDbContext(fixture.Options))
        {
            var store = new PostgresConversationStore(db);
            var dto = await store.StartConversationAsync("Ciudadano Sin Encuesta", "Tramite", "demo-v1", DateTimeOffset.UtcNow);
            conversationId = dto.Id;
            await store.CloseConversationAsync(conversationId, Agent);
        }

        await using var factory = new ConsoleFactory(fixture.ConnectionString);
        var client = CreateAuthorizedClient(factory);

        var conversations = await client.GetFromJsonAsync<List<ConsoleConversationDto>>("/api/console/conversations");
        var conversationDto = Assert.Single(conversations!, c => c.Id == conversationId);

        Assert.Null(conversationDto.FeedbackRating);
        Assert.Null(conversationDto.FeedbackComment);
        Assert.Null(conversationDto.FeedbackCreatedAtUtc);
    }

    [SkippableFact]
    public async Task El_widget_no_recibe_el_comentario_de_feedback()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        var conversationId = await SeedFeedbackAsync(rating: 5, comment: Comment);

        // El historial que consume el widget (mensajes) nunca trae el comentario.
        await using var db = new ChatDbContext(fixture.Options);
        var history = await new PostgresConversationStore(db).GetMessagesAsync(conversationId);
        Assert.NotNull(history);
        Assert.DoesNotContain(history, m => m.Content.Contains(Comment));

        // La auditoria de feedback tampoco contiene el comentario.
        var audit = await db.AuditEvents.SingleAsync(
            a => a.ConversationId == conversationId && a.EventType == "conversation.feedback_submitted");
        Assert.DoesNotContain("Muy clara", audit.DataJson);
    }

    private async Task<Guid> SeedFeedbackAsync(int rating, string? comment)
    {
        Guid conversationId;
        await using (var db = new ChatDbContext(fixture.Options))
        {
            var store = new PostgresConversationStore(db);
            var dto = await store.StartConversationAsync("Ciudadano Feedback Demo", "Seguimiento", "demo-v1", DateTimeOffset.UtcNow);
            conversationId = dto.Id;
            await store.AppendMessageAsync(conversationId, SenderType.Visitor, "Consulta para feedback");
            await store.CloseConversationAsync(conversationId, Agent);
        }

        await using var feedbackFactory = new ConsoleFactory(fixture.ConnectionString);
        var client = feedbackFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/feedback",
            new SubmitFeedbackRequest(rating, comment));
        response.EnsureSuccessStatusCode();
        return conversationId;
    }

    private static HttpClient CreateAuthorizedClient(ConsoleFactory factory)
    {
        var client = factory.CreateClient();
        var sessions = factory.Services.GetRequiredService<IAgentSessionService>();
        var session = sessions.CreateSession(
            new CreateAgentSessionRequest("Funcionario Demo", "test-agent-code"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private sealed class ConsoleFactory(string connectionString) : WebApplicationFactory<Program>
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
