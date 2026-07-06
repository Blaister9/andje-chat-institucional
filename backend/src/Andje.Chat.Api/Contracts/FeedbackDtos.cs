namespace Andje.Chat.Api.Contracts;

/// <summary>Encuesta de satisfaccion enviada por el ciudadano tras el cierre.</summary>
public sealed record SubmitFeedbackRequest(int? Rating, string? Comment);

/// <summary>
/// Respuesta minima al registrar feedback. No re-expone el comentario para
/// mantener la superficie publica pequena.
/// </summary>
public sealed record ConversationFeedbackDto(
    Guid Id,
    Guid ConversationId,
    int Rating,
    DateTimeOffset CreatedAtUtc);
