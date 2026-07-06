using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Andje.Chat.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.ConsoleApi;

public static class ConsoleEndpoints
{
    private const int MaxCannedResponseTitleLength = 80;
    private const int MaxCannedResponseBodyLength = 2000;
    private const int MaxInternalNoteBodyLength = 1000;
    private const int MaxPreviewLength = 140;

    public static IEndpointRouteBuilder MapConsoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/console");

        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/conversations", GetConversationsAsync);
        group.MapGet("/canned-responses", GetCannedResponsesAsync);
        group.MapPost("/canned-responses", CreateCannedResponseAsync);
        group.MapPut("/canned-responses/{id:guid}", UpdateCannedResponseAsync);
        group.MapPatch("/canned-responses/{id:guid}/deactivate", DeactivateCannedResponseAsync);
        group.MapGet("/tags", GetTagsAsync);
        group.MapPost("/conversations/{conversationId:guid}/tags/{tagId:guid}", AssignTagAsync);
        group.MapDelete("/conversations/{conversationId:guid}/tags/{tagId:guid}", RemoveTagAsync);
        group.MapGet("/conversations/{conversationId:guid}/notes", GetNotesAsync);
        group.MapPost("/conversations/{conversationId:guid}/notes", CreateNoteAsync);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out _, out var rejection))
        {
            return rejection;
        }

        var pending = await db.Conversations.CountAsync(
            c => c.Status == ConversationStatus.Pending,
            cancellationToken);
        var active = await db.Conversations.CountAsync(
            c => c.Status == ConversationStatus.Active,
            cancellationToken);
        var closed = await db.Conversations.CountAsync(
            c => c.Status == ConversationStatus.Closed,
            cancellationToken);
        var messages = await db.Messages.CountAsync(cancellationToken);
        var activeResponses = await db.CannedResponses.CountAsync(
            r => r.IsActive,
            cancellationToken);
        var activeTags = await db.ConversationTags.CountAsync(
            t => t.IsActive,
            cancellationToken);

        var feedbackCount = await db.ConversationFeedback.CountAsync(cancellationToken);
        double? averageRating = null;
        int positiveCount = 0;
        double? positiveRate = null;
        if (feedbackCount > 0)
        {
            averageRating = Math.Round(
                await db.ConversationFeedback.AverageAsync(f => (double)f.Rating, cancellationToken), 1);
            positiveCount = await db.ConversationFeedback.CountAsync(f => f.Rating >= 4, cancellationToken);
            positiveRate = Math.Round((double)positiveCount / feedbackCount * 100, 1);
        }

        return Results.Ok(new ConsoleSummaryDto(
            pending + active,
            pending,
            active,
            closed,
            messages,
            activeResponses,
            activeTags,
            feedbackCount,
            averageRating,
            positiveCount,
            positiveRate,
            DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> GetConversationsAsync(
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out _, out var rejection))
        {
            return rejection;
        }

        var conversations = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .Include(c => c.TagAssignments)
                .ThenInclude(a => a.Tag)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        // El comentario de feedback es dato ciudadano; solo se expone aqui
        // (endpoint interno con token de agente), nunca en endpoints publicos.
        var feedback = await db.ConversationFeedback
            .AsNoTracking()
            .Select(f => new { f.ConversationId, f.Rating, f.Comment, f.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        var feedbackByConversation = feedback.ToDictionary(f => f.ConversationId);

        var result = conversations
            .Select(c =>
            {
                var lastMessage = c.Messages
                    .OrderByDescending(m => m.CreatedAtUtc)
                    .FirstOrDefault();
                var tags = c.TagAssignments
                    .Where(a => a.Tag is { IsActive: true })
                    .Select(a => a.Tag!.ToDto())
                    .OrderBy(t => t.Name)
                    .ToList();

                feedbackByConversation.TryGetValue(c.Id, out var fb);

                return new ConsoleConversationDto(
                    c.Id,
                    c.Status.ToString(),
                    c.VisitorDisplayName,
                    c.CreatedAtUtc,
                    c.UpdatedAtUtc,
                    c.ClosedAtUtc,
                    ToPreview(lastMessage?.Body),
                    lastMessage?.CreatedAtUtc,
                    tags,
                    c.Topic,
                    fb?.Rating,
                    fb?.Comment,
                    fb?.CreatedAtUtc);
            })
            .ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCannedResponsesAsync(
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out _, out var rejection))
        {
            return rejection;
        }

        var responses = await db.CannedResponses
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .Select(r => new CannedResponseDto(
                r.Id,
                r.Title,
                r.Body,
                r.IsActive,
                r.SortOrder,
                r.CreatedAtUtc,
                r.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(responses);
    }

    private static async Task<IResult> CreateCannedResponseAsync(
        UpsertCannedResponseRequest request,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var validation = ValidateCannedResponse(request);
        if (validation is not null)
        {
            return validation;
        }

        var now = DateTimeOffset.UtcNow;
        var response = new CannedResponse
        {
            Id = Guid.NewGuid(),
            Title = request.Title!.Trim(),
            Body = request.Body!.Trim(),
            IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? 100,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.CannedResponses.Add(response);
        db.AuditEvents.Add(AuditEvent.For(
            "canned_response.created",
            nameof(SenderType.Agent),
            null,
            new
            {
                cannedResponseId = response.Id,
                title = response.Title,
                agentSessionId = actor.SessionId,
                agentDisplayName = actor.DisplayName,
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/console/canned-responses/{response.Id}", response.ToDto());
    }

    private static async Task<IResult> UpdateCannedResponseAsync(
        Guid id,
        UpsertCannedResponseRequest request,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var validation = ValidateCannedResponse(request);
        if (validation is not null)
        {
            return validation;
        }

        var response = await db.CannedResponses.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (response is null)
        {
            return Results.NotFound();
        }

        response.Title = request.Title!.Trim();
        response.Body = request.Body!.Trim();
        response.IsActive = request.IsActive ?? response.IsActive;
        response.SortOrder = request.SortOrder ?? response.SortOrder;
        response.UpdatedAtUtc = DateTimeOffset.UtcNow;

        db.AuditEvents.Add(AuditEvent.For(
            "canned_response.updated",
            nameof(SenderType.Agent),
            null,
            new
            {
                cannedResponseId = response.Id,
                title = response.Title,
                agentSessionId = actor.SessionId,
                agentDisplayName = actor.DisplayName,
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(response.ToDto());
    }

    private static async Task<IResult> DeactivateCannedResponseAsync(
        Guid id,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var response = await db.CannedResponses.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (response is null)
        {
            return Results.NotFound();
        }

        response.IsActive = false;
        response.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(AuditEvent.For(
            "canned_response.deactivated",
            nameof(SenderType.Agent),
            null,
            new
            {
                cannedResponseId = response.Id,
                title = response.Title,
                agentSessionId = actor.SessionId,
                agentDisplayName = actor.DisplayName,
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(response.ToDto());
    }

    private static async Task<IResult> GetTagsAsync(
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out _, out var rejection))
        {
            return rejection;
        }

        var tags = await db.ConversationTags
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new ConversationTagDto(t.Id, t.Name, t.Color, t.IsActive))
            .ToListAsync(cancellationToken);
        return Results.Ok(tags);
    }

    private static async Task<IResult> AssignTagAsync(
        Guid conversationId,
        Guid tagId,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var conversationExists = await db.Conversations.AnyAsync(
            c => c.Id == conversationId,
            cancellationToken);
        if (!conversationExists)
        {
            return Results.NotFound();
        }

        var tag = await db.ConversationTags.FirstOrDefaultAsync(
            t => t.Id == tagId && t.IsActive,
            cancellationToken);
        if (tag is null)
        {
            return Results.NotFound();
        }

        var exists = await db.ConversationTagAssignments.AnyAsync(
            a => a.ConversationId == conversationId && a.TagId == tagId,
            cancellationToken);
        if (!exists)
        {
            db.ConversationTagAssignments.Add(new ConversationTagAssignment
            {
                ConversationId = conversationId,
                TagId = tagId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            db.AuditEvents.Add(AuditEvent.For(
                "conversation.tag_assigned",
                nameof(SenderType.Agent),
                conversationId,
                new
                {
                    tagId,
                    tagName = tag.Name,
                    agentSessionId = actor.SessionId,
                    agentDisplayName = actor.DisplayName,
                }));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(tag.ToDto());
    }

    private static async Task<IResult> RemoveTagAsync(
        Guid conversationId,
        Guid tagId,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var assignment = await db.ConversationTagAssignments
            .Include(a => a.Tag)
            .FirstOrDefaultAsync(
                a => a.ConversationId == conversationId && a.TagId == tagId,
                cancellationToken);
        if (assignment is null)
        {
            return Results.NoContent();
        }

        db.ConversationTagAssignments.Remove(assignment);
        db.AuditEvents.Add(AuditEvent.For(
            "conversation.tag_removed",
            nameof(SenderType.Agent),
            conversationId,
            new
            {
                tagId,
                tagName = assignment.Tag?.Name,
                agentSessionId = actor.SessionId,
                agentDisplayName = actor.DisplayName,
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetNotesAsync(
        Guid conversationId,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out _, out var rejection))
        {
            return rejection;
        }

        var exists = await db.Conversations.AnyAsync(c => c.Id == conversationId, cancellationToken);
        if (!exists)
        {
            return Results.NotFound();
        }

        var notes = await db.InternalNotes
            .AsNoTracking()
            .Where(n => n.ConversationId == conversationId)
            .OrderBy(n => n.CreatedAtUtc)
            .Select(n => new InternalNoteDto(
                n.Id,
                n.ConversationId,
                n.Body,
                n.AgentDisplayName,
                n.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNoteAsync(
        Guid conversationId,
        CreateInternalNoteRequest request,
        HttpContext context,
        IAgentSessionService sessions,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryRequireAgent(context, sessions, out var actor, out var rejection))
        {
            return rejection;
        }

        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return Results.BadRequest(new { error = "La nota interna es requerida." });
        }

        if (body.Length > MaxInternalNoteBodyLength)
        {
            return Results.BadRequest(new { error = $"La nota no puede superar {MaxInternalNoteBodyLength} caracteres." });
        }

        var exists = await db.Conversations.AnyAsync(c => c.Id == conversationId, cancellationToken);
        if (!exists)
        {
            return Results.NotFound();
        }

        var note = new InternalNote
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Body = body,
            AgentDisplayName = actor.DisplayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.InternalNotes.Add(note);
        db.AuditEvents.Add(AuditEvent.For(
            "internal_note.created",
            nameof(SenderType.Agent),
            conversationId,
            new
            {
                noteId = note.Id,
                agentSessionId = actor.SessionId,
                agentDisplayName = actor.DisplayName,
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/console/conversations/{conversationId}/notes/{note.Id}", note.ToDto());
    }

    private static IResult? ValidateCannedResponse(UpsertCannedResponseRequest request)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return Results.BadRequest(new { error = "El titulo de la respuesta rapida es requerido." });
        }

        if (title.Length > MaxCannedResponseTitleLength)
        {
            return Results.BadRequest(new { error = $"El titulo no puede superar {MaxCannedResponseTitleLength} caracteres." });
        }

        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return Results.BadRequest(new { error = "El cuerpo de la respuesta rapida es requerido." });
        }

        if (body.Length > MaxCannedResponseBodyLength)
        {
            return Results.BadRequest(new { error = $"El cuerpo no puede superar {MaxCannedResponseBodyLength} caracteres." });
        }

        return null;
    }

    private static bool TryRequireAgent(
        HttpContext context,
        IAgentSessionService sessions,
        out AgentActor actor,
        out IResult rejection)
    {
        actor = default!;
        var validation = sessions.ValidateToken(FindBearerToken(context));
        if (!validation.HasToken || validation.Session is null)
        {
            rejection = Results.Unauthorized();
            return false;
        }

        actor = new AgentActor(validation.Session.SessionId, validation.Session.DisplayName);
        rejection = Results.Empty;
        return true;
    }

    private static string? FindBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization["Bearer ".Length..].Trim();
    }

    private static string? ToPreview(string? body)
    {
        var trimmed = body?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxPreviewLength
            ? trimmed
            : $"{trimmed[..MaxPreviewLength]}...";
    }
}
