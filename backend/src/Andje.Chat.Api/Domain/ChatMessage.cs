using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

public sealed record ChatMessage(
    Guid Id,
    Guid ConversationId,
    SenderType SenderType,
    string Content,
    DateTimeOffset SentAt)
{
    public ChatMessageDto ToDto() =>
        new(Id, ConversationId, SenderType.ToString(), Content, SentAt);
}
