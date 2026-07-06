namespace Andje.Chat.Api.Contracts;

public sealed record ConsoleSummaryDto(
    int ConversationsOpen,
    int ConversationsPending,
    int ConversationsActive,
    int ConversationsClosed,
    int MessagesTotal,
    int CannedResponsesActive,
    int TagsActive,
    DateTimeOffset GeneratedAtUtc);

public sealed record ConsoleConversationDto(
    Guid Id,
    string Status,
    string? VisitorDisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string? LastMessagePreview,
    DateTimeOffset? LastMessageAtUtc,
    IReadOnlyList<ConversationTagDto> Tags);

public sealed record CannedResponseDto(
    Guid Id,
    string Title,
    string Body,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertCannedResponseRequest(
    string? Title,
    string? Body,
    int? SortOrder,
    bool? IsActive);

public sealed record ConversationTagDto(
    Guid Id,
    string Name,
    string Color,
    bool IsActive);

public sealed record InternalNoteDto(
    Guid Id,
    Guid ConversationId,
    string Body,
    string AgentDisplayName,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateInternalNoteRequest(string? Body);
