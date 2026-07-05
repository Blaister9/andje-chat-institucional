namespace Andje.Chat.Api.Contracts;

/// <summary>Representación de una conversación hacia los clientes.</summary>
public sealed record ConversationDto(
    Guid Id,
    string Status,
    string? VisitorDisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClosedAtUtc);
