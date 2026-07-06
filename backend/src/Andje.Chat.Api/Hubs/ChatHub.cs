using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Andje.Chat.Api.Hubs;

/// <summary>
/// Hub de chat en tiempo real.
///
/// Grupos:
/// - "agents": conexiones autenticadas de consola. Reciben ConversationStarted,
///   ConversationUpdated, ConversationClosed y una copia de cada MessageReceived.
/// - "conversation:{id}": conexiones del visitante de esa conversacion. Reciben
///   MessageReceived y ConversationClosed de su conversacion.
///
/// Los metodos publicos del visitante quedan abiertos. Las acciones de
/// consola/agente requieren una sesion local de desarrollo validada por token.
/// </summary>
public sealed class ChatHub(
    IConversationStore store,
    IAgentSessionService agentSessions) : Hub
{
    private const string AgentsGroup = "agents";
    private const int MaxMessageLength = 2000;
    private const int MaxDisplayNameLength = 80;
    private const int MaxTopicLength = 60;
    private const int MaxConsentVersionLength = 40;
    private const string DefaultConsentVersion = "demo-v1";

    public string Ping() => "pong";

    /// <summary>El visitante inicia una conversacion y queda unido a ella.</summary>
    public async Task<ConversationDto> StartConversation(StartConversationRequest? request)
    {
        if (request?.ConsentAccepted != true)
        {
            // El widget debe mostrar y capturar la aceptacion del aviso antes de iniciar.
            throw new HubException("Consent is required.");
        }

        var displayName = Normalize(request.DisplayName, MaxDisplayNameLength);
        var topic = Normalize(request.Topic, MaxTopicLength);
        var consentVersion = Normalize(request.ConsentVersion, MaxConsentVersionLength);
        if (string.IsNullOrEmpty(consentVersion))
        {
            consentVersion = DefaultConsentVersion;
        }

        var conversation = await store.StartConversationAsync(
            string.IsNullOrEmpty(displayName) ? null : displayName,
            string.IsNullOrEmpty(topic) ? null : topic,
            consentVersion,
            DateTimeOffset.UtcNow);

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversation.Id));
        await Clients.Group(AgentsGroup).SendAsync("ConversationStarted", conversation);
        return conversation;
    }

    /// <summary>
    /// Une la conexion actual a una conversacion existente (reconexion del
    /// widget) y devuelve el historial para repintar la ventana.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> JoinConversation(Guid conversationId)
    {
        var messages = await store.GetMessagesAsync(conversationId)
            ?? throw new HubException("Conversation not found.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        return messages;
    }

    /// <summary>Devuelve el estado actual de una conversacion existente.</summary>
    public async Task<ConversationDto> GetConversation(Guid conversationId) =>
        await store.GetConversationAsync(conversationId)
            ?? throw new HubException("Conversation not found.");

    /// <summary>
    /// Une la conexion de la consola al grupo de atencion y devuelve las
    /// conversaciones existentes.
    /// </summary>
    public async Task<IReadOnlyList<ConversationDto>> JoinAgentConsole()
    {
        _ = RequireAgentActor();
        await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup);
        return await store.GetConversationsAsync();
    }

    /// <summary>Historial de una conversacion.</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationHistory(Guid conversationId) =>
        await store.GetMessagesAsync(conversationId)
            ?? throw new HubException("Conversation not found.");

    public Task SendVisitorMessage(SendVisitorMessageRequest request) =>
        SendMessageAsync(request.ConversationId, SenderType.Visitor, request.Content);

    public Task SendAgentMessage(SendAgentMessageRequest request)
    {
        var actor = RequireAgentActor();
        return SendMessageAsync(request.ConversationId, SenderType.Agent, request.Content, actor);
    }

    public async Task<ConversationDto> CloseConversation(Guid conversationId)
    {
        var actor = RequireAgentActor();
        var conversation = await store.CloseConversationAsync(conversationId, actor)
            ?? throw new HubException("Conversation not found.");

        await Clients.Group(ConversationGroup(conversationId)).SendAsync("ConversationClosed", conversation);
        await Clients.Group(AgentsGroup).SendAsync("ConversationClosed", conversation);
        await Clients.Group(AgentsGroup).SendAsync("ConversationUpdated", conversation);
        return conversation;
    }

    private async Task SendMessageAsync(
        Guid conversationId,
        SenderType senderType,
        string? content,
        AgentActor? agentActor = null)
    {
        var text = Normalize(content, MaxMessageLength);
        if (string.IsNullOrEmpty(text))
        {
            throw new HubException("El mensaje no puede estar vacio.");
        }

        AppendMessageResult? result;
        try
        {
            result = await store.AppendMessageAsync(conversationId, senderType, text, agentActor);
        }
        catch (ConversationClosedException)
        {
            throw new HubException("Conversation is closed.");
        }

        if (result is null)
        {
            throw new HubException("Conversation not found.");
        }

        // El emisor recibe su propio mensaje por eco del grupo: los clientes
        // pintan solo lo que confirma el servidor.
        await Clients.Group(ConversationGroup(conversationId)).SendAsync("MessageReceived", result.Message);
        await Clients.Group(AgentsGroup).SendAsync("MessageReceived", result.Message);

        if (result.StatusChanged)
        {
            await Clients.Group(AgentsGroup).SendAsync("ConversationUpdated", result.Conversation);
        }
    }

    private static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId}";

    private AgentActor RequireAgentActor()
    {
        var validation = agentSessions.ValidateToken(FindAgentToken());
        if (!validation.HasToken)
        {
            throw new HubException("Agent session is required.");
        }

        if (validation.Session is null)
        {
            throw new HubException("Agent session is invalid or expired.");
        }

        return new AgentActor(validation.Session.SessionId, validation.Session.DisplayName);
    }

    private string? FindAgentToken()
    {
        var httpContext = Context.GetHttpContext();
        var authorization = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return httpContext?.Request.Query["access_token"].FirstOrDefault();
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        throw new HubException($"El texto supera el maximo de {maxLength} caracteres.");
    }
}
