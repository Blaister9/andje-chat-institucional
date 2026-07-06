namespace Andje.Chat.Api.Domain;

/// <summary>
/// Encuesta de satisfaccion del ciudadano al cerrar una conversacion.
/// Un feedback por conversacion (indice unico). El comentario nunca se
/// registra en auditoria ni en logs.
/// </summary>
public class ConversationFeedback
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
