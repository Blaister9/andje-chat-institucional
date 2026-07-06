using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Andje.Chat.Api.Tests;

/// <summary>
/// Pruebas de integración contra PostgreSQL real (base de datos
/// andje_chat_test). Requieren el contenedor de docker-compose:
///   docker compose up -d db
/// Si PostgreSQL no está disponible se omiten (Skip) con un aviso.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly bool _requirePostgres =
        string.Equals(
            Environment.GetEnvironmentVariable("ANDJE_REQUIRE_POSTGRES_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private readonly string _adminConnectionString;
    private readonly string _testDatabase = $"andje_chat_test_{Guid.NewGuid():N}";

    public bool Available { get; private set; }
    public string SkipReason { get; private set; }

    public DbContextOptions<ChatDbContext> Options { get; }
    public string ConnectionString { get; }

    public PostgresFixture()
    {
        var password = Environment.GetEnvironmentVariable("ANDJE_DB_PASSWORD") ?? "andje_dev_local";
        var port = Environment.GetEnvironmentVariable("ANDJE_DB_PORT") ?? "5433";
        SkipReason =
            $"PostgreSQL no está disponible en localhost:{port}. Ejecute 'docker compose up -d db'.";
        _adminConnectionString = $"Host=localhost;Port={port};Database=postgres;Username=andje;Password={password};Timeout=3";
        ConnectionString = $"Host=localhost;Port={port};Database={_testDatabase};Username=andje;Password={password};Timeout=3";
        Options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Base de pruebas recreada desde cero: valida que la migración
            // sea aplicable de forma reproducible.
            await RecreateTestDatabaseAsync();
            await using var db = new ChatDbContext(Options);
            await db.Database.MigrateAsync();
            Available = true;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            Available = false;
            SkipReason = $"{SkipReason} Detalle: {ex.GetBaseException().Message}";
            if (_requirePostgres)
            {
                throw new InvalidOperationException(
                    "PostgreSQL tests are required but the test database is unavailable.",
                    ex);
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (!Available)
        {
            return;
        }

        try
        {
            await DropTestDatabaseAsync();
        }
        catch (Exception)
        {
            // Limpieza best-effort; no debe ocultar el resultado de las pruebas.
        }
    }

    private async Task RecreateTestDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText = $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_testDatabase}' AND pid <> pg_backend_pid();
                """;
            await terminate.ExecuteNonQueryAsync();
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"""DROP DATABASE IF EXISTS "{_testDatabase}";""";
            await drop.ExecuteNonQueryAsync();
        }

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"""CREATE DATABASE "{_testDatabase}";""";
            await create.ExecuteNonQueryAsync();
        }
    }

    private async Task DropTestDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText = $"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_testDatabase}' AND pid <> pg_backend_pid();
                """;
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = connection.CreateCommand();
        drop.CommandText = $"""DROP DATABASE IF EXISTS "{_testDatabase}";""";
        await drop.ExecuteNonQueryAsync();
    }
}

public class PostgresConversationStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly AgentActor Agent = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Agente QA");

    private ChatDbContext CreateContext() => new(fixture.Options);

    [SkippableFact]
    public async Task Iniciar_conversacion_persiste_fila_y_auditoria()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var dto = await new PostgresConversationStore(db).StartConversationAsync("Prueba");

        await using var verify = CreateContext();
        var conversation = await verify.Conversations.SingleAsync(c => c.Id == dto.Id);
        Assert.Equal(ConversationStatus.Pending, conversation.Status);
        Assert.Equal("Prueba", conversation.VisitorDisplayName);
        Assert.Null(conversation.ClosedAtUtc);

        var audit = await verify.AuditEvents.SingleAsync(
            a => a.ConversationId == dto.Id && a.EventType == "conversation.started");
        Assert.Equal("Visitor", audit.ActorType);
    }

    [SkippableFact]
    public async Task Mensaje_de_visitante_se_persiste_con_auditoria_sin_contenido()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var store = new PostgresConversationStore(db);
        var dto = await store.StartConversationAsync(null);
        var result = await store.AppendMessageAsync(dto.Id, SenderType.Visitor, "Texto confidencial del ciudadano");

        Assert.NotNull(result);
        Assert.False(result.StatusChanged);

        await using var verify = CreateContext();
        var message = await verify.Messages.SingleAsync(m => m.ConversationId == dto.Id);
        Assert.Equal("Texto confidencial del ciudadano", message.Body);
        Assert.Equal(SenderType.Visitor, message.SenderType);

        var audit = await verify.AuditEvents.SingleAsync(
            a => a.ConversationId == dto.Id && a.EventType == "message.sent.visitor");
        // La auditoría referencia el mensaje por id pero nunca su contenido.
        Assert.Contains(message.Id.ToString(), audit.DataJson);
        Assert.DoesNotContain("confidencial", audit.DataJson);
    }

    [SkippableFact]
    public async Task Primera_respuesta_del_agente_activa_la_conversacion_con_auditoria()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var store = new PostgresConversationStore(db);
        var dto = await store.StartConversationAsync(null);

        var result = await store.AppendMessageAsync(dto.Id, SenderType.Agent, "Respuesta", Agent);
        Assert.NotNull(result);
        Assert.True(result.StatusChanged);
        Assert.Equal("Active", result.Conversation.Status);

        // Una segunda respuesta ya no cambia el estado.
        var second = await store.AppendMessageAsync(dto.Id, SenderType.Agent, "Otra respuesta", Agent);
        Assert.False(second!.StatusChanged);

        await using var verify = CreateContext();
        var conversation = await verify.Conversations.SingleAsync(c => c.Id == dto.Id);
        Assert.Equal(ConversationStatus.Active, conversation.Status);
        Assert.True(conversation.UpdatedAtUtc >= conversation.CreatedAtUtc);

        Assert.Equal(1, await verify.AuditEvents.CountAsync(
            a => a.ConversationId == dto.Id && a.EventType == "conversation.activated"));
        Assert.Equal(2, await verify.AuditEvents.CountAsync(
            a => a.ConversationId == dto.Id && a.EventType == "message.sent.agent"));

        var audit = await verify.AuditEvents.FirstAsync(
            a => a.ConversationId == dto.Id && a.EventType == "message.sent.agent");
        Assert.Contains("agentSessionId", audit.DataJson);
        Assert.Contains(Agent.SessionId.ToString(), audit.DataJson);
        Assert.Contains("Agente QA", audit.DataJson);
        Assert.DoesNotContain("Respuesta", audit.DataJson);
    }

    [SkippableFact]
    public async Task Cerrar_conversacion_persiste_estado_fecha_y_auditoria_idempotente()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var store = new PostgresConversationStore(db);
        var dto = await store.StartConversationAsync("Cierre");

        var closed = await store.CloseConversationAsync(dto.Id, Agent);
        Assert.NotNull(closed);
        Assert.Equal("Closed", closed.Status);
        Assert.NotNull(closed.ClosedAtUtc);

        var closedAgain = await store.CloseConversationAsync(dto.Id, Agent);
        Assert.NotNull(closedAgain);
        Assert.Equal("Closed", closedAgain.Status);
        Assert.Equal(closed.ClosedAtUtc, closedAgain.ClosedAtUtc);

        await using var verify = CreateContext();
        var conversation = await verify.Conversations.SingleAsync(c => c.Id == dto.Id);
        Assert.Equal(ConversationStatus.Closed, conversation.Status);
        Assert.NotNull(conversation.ClosedAtUtc);
        Assert.True(conversation.UpdatedAtUtc >= conversation.CreatedAtUtc);

        var audit = await verify.AuditEvents.SingleAsync(
            a => a.ConversationId == dto.Id && a.EventType == "conversation.closed");
        Assert.Equal("Agent", audit.ActorType);
        Assert.Contains("agentSessionId", audit.DataJson);
        Assert.Contains(Agent.SessionId.ToString(), audit.DataJson);
        Assert.Contains("Agente QA", audit.DataJson);
        Assert.DoesNotContain("test-agent-code", audit.DataJson);
    }

    [SkippableFact]
    public async Task No_permite_mensajes_en_conversacion_cerrada_y_conserva_historial()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var store = new PostgresConversationStore(db);
        var dto = await store.StartConversationAsync(null);
        var firstMessage = await store.AppendMessageAsync(dto.Id, SenderType.Visitor, "Mensaje inicial");
        Assert.NotNull(firstMessage);

        await store.CloseConversationAsync(dto.Id, Agent);

        await Assert.ThrowsAsync<ConversationClosedException>(() =>
            store.AppendMessageAsync(dto.Id, SenderType.Visitor, "Mensaje posterior"));
        await Assert.ThrowsAsync<ConversationClosedException>(() =>
            store.AppendMessageAsync(dto.Id, SenderType.Agent, "Respuesta posterior", Agent));

        var history = await store.GetMessagesAsync(dto.Id);
        Assert.NotNull(history);
        var message = Assert.Single(history);
        Assert.Equal("Mensaje inicial", message.Content);

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Messages.CountAsync(m => m.ConversationId == dto.Id));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(
            a => a.ConversationId == dto.Id && a.EventType == "conversation.closed"));
    }

    [SkippableFact]
    public async Task Los_datos_sobreviven_a_un_reinicio_simulado_del_proceso()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        Guid conversationId;
        await using (var db = CreateContext())
        {
            var store = new PostgresConversationStore(db);
            var dto = await store.StartConversationAsync("Persistente");
            conversationId = dto.Id;
            await store.AppendMessageAsync(conversationId, SenderType.Visitor, "Mensaje que debe sobrevivir");
        }

        // Contexto y store nuevos = proceso nuevo desde el punto de vista del dominio.
        await using var freshDb = CreateContext();
        var freshStore = new PostgresConversationStore(freshDb);

        var conversations = await freshStore.GetConversationsAsync();
        Assert.Contains(conversations, c => c.Id == conversationId);

        var messages = await freshStore.GetMessagesAsync(conversationId);
        Assert.NotNull(messages);
        var message = Assert.Single(messages);
        Assert.Equal("Mensaje que debe sobrevivir", message.Content);
    }

    [SkippableFact]
    public async Task Conversacion_inexistente_devuelve_null()
    {
        Skip.IfNot(fixture.Available, fixture.SkipReason);

        await using var db = CreateContext();
        var store = new PostgresConversationStore(db);

        Assert.Null(await store.GetMessagesAsync(Guid.NewGuid()));
        Assert.Null(await store.AppendMessageAsync(Guid.NewGuid(), SenderType.Visitor, "Hola"));
    }
}
