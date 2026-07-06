namespace Andje.Chat.Api.Diagnostics;

public sealed record DiagnosticsStatus(
    string Status,
    string Environment,
    string Database,
    DateTimeOffset UtcNow,
    string Version,
    OperationalCounts? Counts);

public sealed record OperationalCounts(
    int ConversationsTotal,
    int ConversationsOpen,
    int ConversationsClosed,
    int MessagesTotal,
    int AuditEventsTotal);
