using System.Text.Json;

namespace Andje.Chat.Api.Domain;

/// <summary>
/// Evento de auditoría, solo inserción. DataJson lleva referencias (ids),
/// nunca contenido de mensajes ni datos personales.
/// </summary>
public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid? ConversationId { get; set; }
    public string EventType { get; set; } = string.Empty;

    /// <summary>"Visitor" | "Agent" | "System".</summary>
    public string ActorType { get; set; } = string.Empty;

    public string? DataJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public static AuditEvent For(
        string eventType,
        string actorType,
        Guid? conversationId,
        object? data = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        ActorType = actorType,
        ConversationId = conversationId,
        DataJson = data is null ? null : JsonSerializer.Serialize(data),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
