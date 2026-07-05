using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

/// <summary>
/// Entidad persistida de una conversación.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; }
    public string? VisitorDisplayName { get; set; }
    public ConversationStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];

    public ConversationDto ToDto() =>
        new(Id, Status.ToString(), VisitorDisplayName, CreatedAtUtc, UpdatedAtUtc, ClosedAtUtc);
}
