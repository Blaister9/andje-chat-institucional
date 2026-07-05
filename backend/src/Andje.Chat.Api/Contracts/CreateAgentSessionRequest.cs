namespace Andje.Chat.Api.Contracts;

public sealed record CreateAgentSessionRequest(
    string? DisplayName,
    string? AccessCode);
