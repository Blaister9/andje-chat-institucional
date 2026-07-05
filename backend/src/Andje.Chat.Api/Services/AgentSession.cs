namespace Andje.Chat.Api.Services;

public sealed record AgentSession(
    Guid SessionId,
    string DisplayName,
    DateTimeOffset ExpiresAtUtc);
