using System.Collections.Concurrent;
using Andje.Chat.Api.Domain;

namespace Andje.Chat.Api.Services;

/// <summary>
/// Almacenamiento temporal en memoria para desarrollo local. Se pierde al
/// reiniciar el proceso; la fase de persistencia lo reemplaza por PostgreSQL
/// detrás de esta misma superficie.
/// </summary>
public sealed class InMemoryConversationStore
{
    private readonly ConcurrentDictionary<Guid, Conversation> _conversations = new();

    public Conversation Start(string? visitorDisplayName)
    {
        var conversation = new Conversation(visitorDisplayName);
        _conversations[conversation.Id] = conversation;
        return conversation;
    }

    public bool TryGet(Guid id, out Conversation conversation)
    {
        var found = _conversations.TryGetValue(id, out var value);
        conversation = value!;
        return found;
    }

    public IReadOnlyList<Conversation> GetAll() =>
        [.. _conversations.Values.OrderBy(c => c.StartedAt)];
}
