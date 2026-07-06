using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.PublicApi;

/// <summary>
/// Endpoints publicos del ciudadano. No requieren token de agente. Hoy solo
/// exponen la encuesta de satisfaccion posterior al cierre. Nunca devuelven
/// notas internas, etiquetas internas ni configuracion de agente.
/// </summary>
public static class CitizenEndpoints
{
    private const int MaxCommentLength = 500;

    public static IEndpointRouteBuilder MapCitizenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/{conversationId:guid}/feedback", SubmitFeedbackAsync);
        return app;
    }

    private static async Task<IResult> SubmitFeedbackAsync(
        Guid conversationId,
        SubmitFeedbackRequest request,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.Rating is not (>= 1 and <= 5))
        {
            return Results.BadRequest(new { error = "La calificacion debe estar entre 1 y 5." });
        }

        var comment = request.Comment?.Trim();
        if (comment is { Length: > MaxCommentLength })
        {
            return Results.BadRequest(new { error = $"El comentario no puede superar {MaxCommentLength} caracteres." });
        }

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound();
        }

        // La encuesta solo se habilita cuando la conversacion esta cerrada.
        if (conversation.Status != ConversationStatus.Closed)
        {
            return Results.Conflict(new { error = "La conversacion aun no esta cerrada." });
        }

        var alreadyExists = await db.ConversationFeedback
            .AnyAsync(f => f.ConversationId == conversationId, cancellationToken);
        if (alreadyExists)
        {
            return Results.Conflict(new { error = "Esta conversacion ya tiene una encuesta registrada." });
        }

        var feedback = new ConversationFeedback
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Rating = request.Rating.Value,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.ConversationFeedback.Add(feedback);

        // Auditoria: solo rating y feedbackId. El comentario nunca se registra.
        db.AuditEvents.Add(AuditEvent.For(
            "conversation.feedback_submitted",
            nameof(SenderType.Visitor),
            conversationId,
            new { feedbackId = feedback.Id, rating = feedback.Rating }));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Carrera contra el indice unico: otra encuesta llego primero.
            return Results.Conflict(new { error = "Esta conversacion ya tiene una encuesta registrada." });
        }

        return Results.Created(
            $"/api/conversations/{conversationId}/feedback",
            new ConversationFeedbackDto(feedback.Id, conversationId, feedback.Rating, feedback.CreatedAtUtc));
    }
}
