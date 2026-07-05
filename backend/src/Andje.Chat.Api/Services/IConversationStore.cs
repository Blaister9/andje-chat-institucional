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
        string? visitorDisplayName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        CancellationToken cancellationToken = default);

    /// <returns>Los mensajes en orden cronológico, o null si la conversación no existe.</returns>
    Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(
        Guid conversationId, CancellationToken cancellationToken = default);

    /// <returns>El resultado del envío, o null si la conversación no existe.</returns>
    Task<AppendMessageResult?> AppendMessageAsync(
        Guid conversationId, SenderType senderType, string body,
        CancellationToken cancellationToken = default);
}

/// <param name="StatusChanged">True si la conversación pasó de Pending a Active.</param>
public sealed record AppendMessageResult(
    ChatMessageDto Message,
    ConversationDto Conversation,
    bool StatusChanged);
