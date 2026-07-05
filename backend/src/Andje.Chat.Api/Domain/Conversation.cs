using Andje.Chat.Api.Contracts;

namespace Andje.Chat.Api.Domain;

/// <summary>
/// Conversación en memoria. Thread-safe: los accesos a mensajes y estado se
/// serializan con un lock interno (varias conexiones SignalR pueden escribir
/// a la vez).
/// </summary>
public sealed class Conversation
{
    private readonly object _sync = new();
    private readonly List<ChatMessage> _messages = [];

    public Conversation(string? visitorDisplayName)
    {
        Id = Guid.NewGuid();
        VisitorDisplayName = visitorDisplayName;
        StartedAt = DateTimeOffset.UtcNow;
        Status = ConversationStatus.Pending;
    }

    public Guid Id { get; }
    public string? VisitorDisplayName { get; }
    public DateTimeOffset StartedAt { get; }
    public ConversationStatus Status { get; private set; }

    /// <summary>
    /// Agrega un mensaje. La primera respuesta de un agente pasa la
    /// conversación de Pending a Active; devuelve si el estado cambió para
    /// que el hub notifique a la consola.
    /// </summary>
    public (ChatMessage Message, bool StatusChanged) AddMessage(SenderType senderType, string content)
    {
        lock (_sync)
        {
            var message = new ChatMessage(Guid.NewGuid(), Id, senderType, content, DateTimeOffset.UtcNow);
            _messages.Add(message);

            var statusChanged = false;
            if (senderType == SenderType.Agent && Status == ConversationStatus.Pending)
            {
                Status = ConversationStatus.Active;
                statusChanged = true;
            }

            return (message, statusChanged);
        }
    }

    public IReadOnlyList<ChatMessage> GetMessages()
    {
        lock (_sync)
        {
            return [.. _messages];
        }
    }

    public ConversationDto ToDto()
    {
        lock (_sync)
        {
            return new ConversationDto(Id, Status.ToString(), VisitorDisplayName, StartedAt);
        }
    }
}
