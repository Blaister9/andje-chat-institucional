namespace Andje.Chat.Api.Contracts;

/// <summary>Mensaje enviado por el ciudadano desde el widget.</summary>
public sealed record SendVisitorMessageRequest(Guid ConversationId, string? Content);
