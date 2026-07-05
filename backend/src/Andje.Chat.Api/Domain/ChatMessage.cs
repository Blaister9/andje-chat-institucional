using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

/// <summary>
/// Mensaje persistido. Inmutable por regla de negocio: la aplicación nunca lo
/// actualiza ni lo borra después de insertarlo.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public SenderType SenderType { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    // El contrato hacia los clientes conserva los nombres de la fase 01
    // (Content/SentAt) para no romper widget ni consola.
    public ChatMessageDto ToDto() =>
        new(Id, ConversationId, SenderType.ToString(), Body, CreatedAtUtc);
}
