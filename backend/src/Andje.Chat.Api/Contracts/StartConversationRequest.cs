namespace Andje.Chat.Api.Contracts;

/// <summary>Solicitud del widget para iniciar una conversación.</summary>
public sealed record StartConversationRequest(string? DisplayName);
