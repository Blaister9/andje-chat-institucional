using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

public class CannedResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public CannedResponseDto ToDto() =>
        new(Id, Title, Body, IsActive, SortOrder, CreatedAtUtc, UpdatedAtUtc);
}
