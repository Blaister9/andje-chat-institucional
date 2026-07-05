using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Andje.Chat.Api.Hubs;

/// <summary>
/// Hub de chat en tiempo real.
///
/// Grupos:
/// - "agents": todas las conexiones de la consola. Reciben
///   ConversationStarted, ConversationUpdated y una copia de cada
///   MessageReceived.
/// - "conversation:{id}": la(s) conexión(es) del visitante de esa
///   conversación. Reciben MessageReceived de su conversación.
///
/// Sin autenticación en esta fase: cualquier conexión puede actuar como
/// visitante o consola. Bloqueante a resolver antes de exponer fuera de
/// localhost (ver docs/privacy-security-baseline.md).
/// </summary>
public sealed class ChatHub(IConversationStore store) : Hub
{
    private const string AgentsGroup = "agents";
    private const int MaxMessageLength = 2000;
    private const int MaxDisplayNameLength = 80;

    public string Ping() => "pong";

    /// <summary>El visitante inicia una conversación y queda unido a ella.</summary>
    public async Task<ConversationDto> StartConversation(StartConversationRequest? request)
    {
        var displayName = Normalize(request?.DisplayName, MaxDisplayNameLength);
        var conversation = await store.StartConversationAsync(
            string.IsNullOrEmpty(displayName) ? null : displayName);

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversation.Id));
        await Clients.Group(AgentsGroup).SendAsync("ConversationStarted", conversation);
        return conversation;
    }

    /// <summary>
    /// Une la conexión actual a una conversación existente (reconexión del
    /// widget) y devuelve el historial para repintar la ventana.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> JoinConversation(Guid conversationId)
    {
        var messages = await store.GetMessagesAsync(conversationId)
            ?? throw new HubException("La conversación no existe.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        return messages;
    }

    /// <summary>
    /// Une la conexión de la consola al grupo de atención y devuelve las
    /// conversaciones existentes.
    /// </summary>
    public async Task<IReadOnlyList<ConversationDto>> JoinAgentConsole()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup);
        return await store.GetConversationsAsync();
    }

    /// <summary>Historial de una conversación (la consola lo pide al seleccionarla).</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationHistory(Guid conversationId) =>
        await store.GetMessagesAsync(conversationId)
            ?? throw new HubException("La conversación no existe.");

    public Task SendVisitorMessage(SendVisitorMessageRequest request) =>
        SendMessageAsync(request.ConversationId, SenderType.Visitor, request.Content);

    public Task SendAgentMessage(SendAgentMessageRequest request) =>
        SendMessageAsync(request.ConversationId, SenderType.Agent, request.Content);

    private async Task SendMessageAsync(Guid conversationId, SenderType senderType, string? content)
    {
        var text = Normalize(content, MaxMessageLength);
        if (string.IsNullOrEmpty(text))
        {
            throw new HubException("El mensaje no puede estar vacío.");
        }

        var result = await store.AppendMessageAsync(conversationId, senderType, text)
            ?? throw new HubException("La conversación no existe.");

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

    private static string? Normalize(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        throw new HubException($"El texto supera el máximo de {maxLength} caracteres.");
    }
}
