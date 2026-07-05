using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.Services;

/// <summary>
/// Implementación persistente sobre PostgreSQL. Cada operación de escritura
/// guarda entidad + eventos de auditoría en un único SaveChanges (una sola
/// transacción).
/// </summary>
public sealed class PostgresConversationStore(ChatDbContext db) : IConversationStore
{
    public async Task<ConversationDto> StartConversationAsync(
        string? visitorDisplayName, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            VisitorDisplayName = visitorDisplayName,
            Status = ConversationStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Conversations.Add(conversation);
        db.AuditEvents.Add(AuditEvent.For(
            "conversation.started", nameof(SenderType.Visitor), conversation.Id));

        await db.SaveChangesAsync(cancellationToken);
        return conversation.ToDto();
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        var conversations = await db.Conversations
            .AsNoTracking()
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return [.. conversations.Select(c => c.ToDto())];
    }

    public async Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var messages = await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return [.. messages.Select(m => m.ToDto())];
    }

    public async Task<AppendMessageResult?> AppendMessageAsync(
        Guid conversationId, SenderType senderType, string body,
        CancellationToken cancellationToken = default)
    {
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderType = senderType,
            Body = body,
            CreatedAtUtc = now,
        };
        db.Messages.Add(message);

        var actor = senderType.ToString();
        // Auditoría con referencias (ids), nunca con el contenido del mensaje.
        db.AuditEvents.Add(AuditEvent.For(
            $"message.sent.{actor.ToLowerInvariant()}", actor, conversationId,
            new { messageId = message.Id }));

        var statusChanged = false;
        if (senderType == SenderType.Agent && conversation.Status == ConversationStatus.Pending)
        {
            conversation.Status = ConversationStatus.Active;
            statusChanged = true;
            db.AuditEvents.Add(AuditEvent.For(
                "conversation.activated", actor, conversationId,
                new { messageId = message.Id }));
        }

        conversation.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return new AppendMessageResult(message.ToDto(), conversation.ToDto(), statusChanged);
    }
}
