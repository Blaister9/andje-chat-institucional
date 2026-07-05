using Andje.Chat.Api.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Andje.Chat.Api.Services;

namespace Andje.Chat.Api.Tests;

public class RealtimeFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Flujo_completo_visitante_y_consola_en_tiempo_real()
    {
        await using var visitor = CreateConnection();
        await using var agent = CreateConnection(CreateAgentToken());
        await visitor.StartAsync();
        await agent.StartAsync();

        // La consola se une al grupo de atención antes de que exista la conversación.
        var conversationStarted = NewEventSource<ConversationDto>();
        agent.On<ConversationDto>("ConversationStarted", dto => conversationStarted.TrySetResult(dto));
        await agent.InvokeAsync<List<ConversationDto>>("JoinAgentConsole");

        // El visitante inicia la conversación; la consola es notificada sin refrescar.
        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation", new StartConversationRequest("Ciudadano de prueba"));

        Assert.Equal("Pending", conversation.Status);
        var notified = await conversationStarted.Task.WaitAsync(EventTimeout);
        Assert.Equal(conversation.Id, notified.Id);

        // El visitante envía un mensaje; la consola lo recibe en tiempo real.
        var agentReceived = NewEventSource<ChatMessageDto>();
        agent.On<ChatMessageDto>("MessageReceived", dto =>
        {
            if (dto.ConversationId == conversation.Id && dto.SenderType == "Visitor")
            {
                agentReceived.TrySetResult(dto);
            }
        });

        var visitorReceivedReply = NewEventSource<ChatMessageDto>();
        visitor.On<ChatMessageDto>("MessageReceived", dto =>
        {
            if (dto.ConversationId == conversation.Id && dto.SenderType == "Agent")
            {
                visitorReceivedReply.TrySetResult(dto);
            }
        });

        var conversationUpdated = NewEventSource<ConversationDto>();
        agent.On<ConversationDto>("ConversationUpdated", dto => conversationUpdated.TrySetResult(dto));

        await visitor.InvokeAsync("SendVisitorMessage",
            new SendVisitorMessageRequest(conversation.Id, "Hola, necesito información"));

        var received = await agentReceived.Task.WaitAsync(EventTimeout);
        Assert.Equal("Hola, necesito información", received.Content);

        // El agente responde; el widget recibe la respuesta en tiempo real
        // y la conversación pasa de Pending a Active.
        await agent.InvokeAsync("SendAgentMessage",
            new SendAgentMessageRequest(conversation.Id, "Con gusto, ¿en qué puedo ayudarle?"));

        var reply = await visitorReceivedReply.Task.WaitAsync(EventTimeout);
        Assert.Equal("Con gusto, ¿en qué puedo ayudarle?", reply.Content);

        var updated = await conversationUpdated.Task.WaitAsync(EventTimeout);
        Assert.Equal("Active", updated.Status);
    }

    [Fact]
    public async Task El_historial_se_recupera_al_unirse_a_la_conversacion()
    {
        await using var visitor = CreateConnection();
        await visitor.StartAsync();

        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation", new StartConversationRequest(null));
        await visitor.InvokeAsync("SendVisitorMessage",
            new SendVisitorMessageRequest(conversation.Id, "Primer mensaje"));

        // Simula una reconexión del widget con una conexión nueva.
        await using var reconnected = CreateConnection();
        await reconnected.StartAsync();
        var history = await reconnected.InvokeAsync<List<ChatMessageDto>>(
            "JoinConversation", conversation.Id);

        var message = Assert.Single(history);
        Assert.Equal("Primer mensaje", message.Content);
        Assert.Equal("Visitor", message.SenderType);
    }

    [Fact]
    public async Task Cerrar_conversacion_notifica_bloquea_mensajes_y_conserva_historial()
    {
        await using var visitor = CreateConnection();
        await using var agent = CreateConnection(CreateAgentToken());
        await visitor.StartAsync();
        await agent.StartAsync();
        await agent.InvokeAsync<List<ConversationDto>>("JoinAgentConsole");

        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation", new StartConversationRequest("Ciudadano de prueba"));
        await visitor.InvokeAsync("SendVisitorMessage",
            new SendVisitorMessageRequest(conversation.Id, "Mensaje antes del cierre"));

        var visitorClosed = NewEventSource<ConversationDto>();
        visitor.On<ConversationDto>("ConversationClosed", dto =>
        {
            if (dto.Id == conversation.Id)
            {
                visitorClosed.TrySetResult(dto);
            }
        });

        var agentClosed = NewEventSource<ConversationDto>();
        agent.On<ConversationDto>("ConversationClosed", dto =>
        {
            if (dto.Id == conversation.Id)
            {
                agentClosed.TrySetResult(dto);
            }
        });

        var closed = await agent.InvokeAsync<ConversationDto>("CloseConversation", conversation.Id);
        Assert.Equal("Closed", closed.Status);
        Assert.NotNull(closed.ClosedAtUtc);

        var visitorNotification = await visitorClosed.Task.WaitAsync(EventTimeout);
        var agentNotification = await agentClosed.Task.WaitAsync(EventTimeout);
        Assert.Equal("Closed", visitorNotification.Status);
        Assert.Equal("Closed", agentNotification.Status);

        await Assert.ThrowsAsync<HubException>(() => visitor.InvokeAsync(
            "SendVisitorMessage", new SendVisitorMessageRequest(conversation.Id, "No debe enviarse")));
        await Assert.ThrowsAsync<HubException>(() => agent.InvokeAsync(
            "SendAgentMessage", new SendAgentMessageRequest(conversation.Id, "No debe enviarse")));

        await using var reconnected = CreateConnection();
        await reconnected.StartAsync();
        var history = await reconnected.InvokeAsync<List<ChatMessageDto>>(
            "JoinConversation", conversation.Id);
        var message = Assert.Single(history);
        Assert.Equal("Mensaje antes del cierre", message.Content);

        var current = await reconnected.InvokeAsync<ConversationDto>(
            "GetConversation", conversation.Id);
        Assert.Equal("Closed", current.Status);
    }

    [Fact]
    public async Task Un_mensaje_vacio_es_rechazado()
    {
        await using var visitor = CreateConnection();
        await visitor.StartAsync();

        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation", new StartConversationRequest(null));

        await Assert.ThrowsAsync<HubException>(() => visitor.InvokeAsync(
            "SendVisitorMessage", new SendVisitorMessageRequest(conversation.Id, "   ")));
    }

    [Fact]
    public async Task Enviar_a_una_conversacion_inexistente_es_rechazado()
    {
        await using var agent = CreateConnection(CreateAgentToken());
        await agent.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => agent.InvokeAsync(
            "SendAgentMessage", new SendAgentMessageRequest(Guid.NewGuid(), "Hola")));
    }

    [Fact]
    public async Task Metodos_de_agente_sin_token_son_rechazados()
    {
        await using var client = CreateConnection();
        await client.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            client.InvokeAsync<List<ConversationDto>>("JoinAgentConsole"));
        await Assert.ThrowsAsync<HubException>(() => client.InvokeAsync(
            "SendAgentMessage", new SendAgentMessageRequest(Guid.NewGuid(), "Hola")));
        await Assert.ThrowsAsync<HubException>(() =>
            client.InvokeAsync<ConversationDto>("CloseConversation", Guid.NewGuid()));
    }

    [Fact]
    public async Task Metodos_de_agente_con_token_invalido_son_rechazados()
    {
        await using var client = CreateConnection("token-invalido");
        await client.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            client.InvokeAsync<List<ConversationDto>>("JoinAgentConsole"));
    }

    [Fact]
    public async Task Metodos_publicos_de_visitante_funcionan_sin_token()
    {
        await using var visitor = CreateConnection();
        await visitor.StartAsync();

        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation", new StartConversationRequest("Visitante sin token"));
        await visitor.InvokeAsync("SendVisitorMessage",
            new SendVisitorMessageRequest(conversation.Id, "Mensaje visitante"));

        var history = await visitor.InvokeAsync<List<ChatMessageDto>>(
            "JoinConversation", conversation.Id);
        Assert.Single(history);
    }

    private string CreateAgentToken()
    {
        var sessions = factory.Services.GetRequiredService<IAgentSessionService>();
        var response = sessions.CreateSession(
            new CreateAgentSessionRequest("Agente QA", "test-agent-code"));
        return response.AccessToken;
    }

    private HubConnection CreateConnection(string? accessToken = null) => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/chat"), options =>
        {
            // TestServer no soporta WebSockets; long polling basta para las pruebas.
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            }
        })
        .Build();

    private static TaskCompletionSource<T> NewEventSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
