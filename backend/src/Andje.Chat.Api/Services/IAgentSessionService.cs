using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Services;

public interface IAgentSessionService
{
    CreateAgentSessionResponse CreateSession(CreateAgentSessionRequest request);

    AgentSessionValidationResult ValidateToken(string? token);
}

public sealed record AgentSessionValidationResult(
    bool HasToken,
    AgentSession? Session)
{
    public bool IsValid => Session is not null;
}
