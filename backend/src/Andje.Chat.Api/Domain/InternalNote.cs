using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

public class InternalNote
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string AgentDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Conversation? Conversation { get; set; }

    public InternalNoteDto ToDto() =>
        new(Id, ConversationId, Body, AgentDisplayName, CreatedAtUtc);
}
