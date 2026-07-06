using System.Collections.Concurrent;
using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Domain;

namespace Andje.Chat.Api.Services;

/// <summary>
/// Implementación en memoria de IConversationStore. Se usa en las pruebas
/// del flujo realtime para no depender de PostgreSQL; en ejecución normal la
/// implementación registrada es PostgresConversationStore.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private sealed class Entry(ConversationDto conversation)
    {
        public ConversationDto Conversation { get; set; } = conversation;
        public List<ChatMessageDto> Messages { get; } = [];
    }

    private readonly ConcurrentDictionary<Guid, Entry> _conversations = new();
    private readonly object _sync = new();

    public Task<ConversationDto> StartConversationAsync(
        string? visitorDisplayName,
        string? topic = null,
        string? consentVersion = null,
        DateTimeOffset? consentAcceptedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        var dto = new ConversationDto(
            Guid.NewGuid(),
            ConversationStatus.Pending.ToString(),
            visitorDisplayName,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            topic);
        _conversations[dto.Id] = new Entry(dto);
        return Task.FromResult(dto);
    }

    public Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConversationDto> result;
        lock (_sync)
        {
            result = [.. _conversations.Values
                .Select(e => e.Conversation)
                .OrderBy(c => c.StartedAt)];
        }
        return Task.FromResult(result);
    }

    public Task<ConversationDto?> GetConversationAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var entry))
        {
            return Task.FromResult<ConversationDto?>(null);
        }

        lock (_sync)
        {
            return Task.FromResult<ConversationDto?>(entry.Conversation);
        }
    }

    public Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var entry))
        {
            return Task.FromResult<IReadOnlyList<ChatMessageDto>?>(null);
        }

        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ChatMessageDto>?>([.. entry.Messages]);
        }
    }

    public Task<AppendMessageResult?> AppendMessageAsync(
        Guid conversationId, SenderType senderType, string body,
        AgentActor? agentActor = null,
        CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var entry))
        {
            return Task.FromResult<AppendMessageResult?>(null);
        }

        lock (_sync)
        {
            if (entry.Conversation.Status == ConversationStatus.Closed.ToString())
            {
                throw new ConversationClosedException();
            }

            var now = DateTimeOffset.UtcNow;
            var message = new ChatMessageDto(
                Guid.NewGuid(), conversationId, senderType.ToString(), body, now);
            entry.Messages.Add(message);

            var statusChanged = false;
            if (senderType == SenderType.Agent &&
                entry.Conversation.Status == ConversationStatus.Pending.ToString())
            {
                entry.Conversation = entry.Conversation with
                {
                    Status = ConversationStatus.Active.ToString(),
                    UpdatedAtUtc = now,
                };
                statusChanged = true;
            }
            else
            {
                entry.Conversation = entry.Conversation with
                {
                    UpdatedAtUtc = now,
                };
            }

            return Task.FromResult<AppendMessageResult?>(
                new AppendMessageResult(message, entry.Conversation, statusChanged));
        }
    }

    public Task<ConversationDto?> CloseConversationAsync(
        Guid conversationId, AgentActor agentActor,
        CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var entry))
        {
            return Task.FromResult<ConversationDto?>(null);
        }

        lock (_sync)
        {
            if (entry.Conversation.Status == ConversationStatus.Closed.ToString())
            {
                return Task.FromResult<ConversationDto?>(entry.Conversation);
            }

            var now = DateTimeOffset.UtcNow;
            entry.Conversation = entry.Conversation with
            {
                Status = ConversationStatus.Closed.ToString(),
                UpdatedAtUtc = now,
                ClosedAtUtc = now,
            };
            return Task.FromResult<ConversationDto?>(entry.Conversation);
        }
    }
}
