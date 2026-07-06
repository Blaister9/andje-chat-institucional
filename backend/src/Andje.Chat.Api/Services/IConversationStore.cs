using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Domain;

namespace Andje.Chat.Api.Services;

/// <summary>
/// Abstracción del almacenamiento de conversaciones. Implementaciones:
/// PostgresConversationStore (ejecución normal) e InMemoryConversationStore
/// (pruebas del flujo realtime sin base de datos).
/// </summary>
public interface IConversationStore
{
    Task<ConversationDto> StartConversationAsync(
        string? visitorDisplayName,
        string? topic = null,
        string? consentVersion = null,
        DateTimeOffset? consentAcceptedAtUtc = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        CancellationToken cancellationToken = default);

    /// <returns>La conversación, o null si no existe.</returns>
    Task<ConversationDto?> GetConversationAsync(
        Guid conversationId, CancellationToken cancellationToken = default);

    /// <returns>Los mensajes en orden cronológico, o null si la conversación no existe.</returns>
    Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(
        Guid conversationId, CancellationToken cancellationToken = default);

    /// <returns>El resultado del envío, o null si la conversación no existe.</returns>
    Task<AppendMessageResult?> AppendMessageAsync(
        Guid conversationId, SenderType senderType, string body,
        AgentActor? agentActor = null,
        CancellationToken cancellationToken = default);

    /// <returns>La conversación cerrada, o null si no existe.</returns>
    Task<ConversationDto?> CloseConversationAsync(
        Guid conversationId, AgentActor agentActor,
        CancellationToken cancellationToken = default);
}

/// <param name="StatusChanged">True si la conversación pasó de Pending a Active.</param>
public sealed record AppendMessageResult(
    ChatMessageDto Message,
    ConversationDto Conversation,
    bool StatusChanged);

public sealed class ConversationClosedException : InvalidOperationException
{
    public ConversationClosedException() : base("Conversation is closed.")
    {
    }
}
