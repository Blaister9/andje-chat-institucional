namespace Andje.Chat.Api.Contracts;

/// <summary>Mensaje enviado por el agente desde la consola.</summary>
public sealed record SendAgentMessageRequest(Guid ConversationId, string? Content);
