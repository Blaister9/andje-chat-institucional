namespace Andje.Chat.Api.Contracts;

/// <summary>Mensaje de chat hacia los clientes. SenderType: "Visitor" | "Agent".</summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string SenderType,
    string Content,
    DateTimeOffset SentAt);
