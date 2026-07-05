using System.Collections.Concurrent;
using System.Security.Cryptography;
using Andje.Chat.Api.Contracts;
using Microsoft.Extensions.Options;

namespace Andje.Chat.Api.Services;

public sealed class AgentSessionService(
    IOptions<AgentAccessOptions> options,
    TimeProvider timeProvider) : IAgentSessionService
{
    private const int MaxDisplayNameLength = 80;
    // Cota superior defensiva: un codigo legitimo es corto; nada mas largo se
    // compara para no gastar trabajo con payloads abusivos.
    private const int MaxAccessCodeLength = 256;
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public CreateAgentSessionResponse CreateSession(CreateAgentSessionRequest request)
    {
        var currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            throw new AgentSessionRejectedException("Agent access is disabled.");
        }

        var configuredCode = currentOptions.DevelopmentAccessCode;
        if (string.IsNullOrWhiteSpace(configuredCode))
        {
            throw new AgentSessionRejectedException("Agent access is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.AccessCode) ||
            request.AccessCode.Length > MaxAccessCodeLength ||
            !AccessCodeMatches(configuredCode, request.AccessCode))
        {
            throw new AgentSessionRejectedException("Invalid access code.");
        }

        var displayName = NormalizeDisplayName(request.DisplayName);
        var expiresAtUtc = timeProvider.GetUtcNow()
            .AddMinutes(Math.Max(1, currentOptions.SessionMinutes));
        var token = GenerateToken();
        var session = new AgentSession(Guid.NewGuid(), displayName, expiresAtUtc);
        _sessions[token] = session;

        return new CreateAgentSessionResponse(token, displayName, expiresAtUtc);
    }

    public AgentSessionValidationResult ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AgentSessionValidationResult(false, null);
        }

        if (!_sessions.TryGetValue(token, out var session))
        {
            return new AgentSessionValidationResult(true, null);
        }

        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            _sessions.TryRemove(token, out _);
            return new AgentSessionValidationResult(true, null);
        }

        return new AgentSessionValidationResult(true, session);
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Agente local";
        }

        return trimmed.Length <= MaxDisplayNameLength
            ? trimmed
            : trimmed[..MaxDisplayNameLength];
    }

    private static bool AccessCodeMatches(string configuredCode, string providedCode)
    {
        var expected = System.Text.Encoding.UTF8.GetBytes(configuredCode);
        var provided = System.Text.Encoding.UTF8.GetBytes(providedCode);
        return expected.Length == provided.Length &&
            CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed class AgentSessionRejectedException(string message) : InvalidOperationException(message);
