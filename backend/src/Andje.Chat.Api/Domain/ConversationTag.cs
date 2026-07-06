using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

public class ConversationTag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#52606d";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<ConversationTagAssignment> Assignments { get; set; } = [];

    public ConversationTagDto ToDto() => new(Id, Name, Color, IsActive);
}
