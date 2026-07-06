namespace Andje.Chat.Api.Domain;

public class ConversationTagAssignment
{
    public Guid ConversationId { get; set; }
    public Guid TagId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Conversation? Conversation { get; set; }
    public ConversationTag? Tag { get; set; }
}
