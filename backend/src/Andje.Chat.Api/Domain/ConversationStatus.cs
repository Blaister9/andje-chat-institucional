namespace Andje.Chat.Api.Domain;

public enum ConversationStatus
{
    /// <summary>Iniciada por el visitante; ningún agente ha respondido.</summary>
    Pending,

    /// <summary>Un agente ya respondió.</summary>
    Active,

    /// <summary>Cerrada por un agente; no acepta nuevos mensajes.</summary>
    Closed
}
