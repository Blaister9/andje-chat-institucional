using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

/// <summary>
/// Entidad persistida de una conversación.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; }
    public string? VisitorDisplayName { get; set; }
    public ConversationStatus Status { get; set; }

    /// <summary>Categoria/tema ciudadano elegido al iniciar (catalogo del widget).</summary>
    public string? Topic { get; set; }

    /// <summary>Momento UTC en que el ciudadano acepto el aviso de tratamiento de datos.</summary>
    public DateTimeOffset? ConsentAcceptedAtUtc { get; set; }

    /// <summary>Version del texto de consentimiento aceptado, por ejemplo "demo-v1".</summary>
    public string? ConsentVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
    public List<InternalNote> InternalNotes { get; set; } = [];
    public List<ConversationTagAssignment> TagAssignments { get; set; } = [];

    public ConversationDto ToDto() =>
        new(Id, Status.ToString(), VisitorDisplayName, CreatedAtUtc, UpdatedAtUtc, ClosedAtUtc, Topic);
}
