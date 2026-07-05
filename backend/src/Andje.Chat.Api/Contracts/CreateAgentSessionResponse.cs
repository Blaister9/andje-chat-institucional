namespace Andje.Chat.Api.Contracts;

public sealed record CreateAgentSessionResponse(
    string AccessToken,
    string AgentDisplayName,
    DateTimeOffset ExpiresAtUtc);
