namespace Andje.Chat.Api.Contracts;

/// <summary>
/// Solicitud del widget para iniciar una conversación. El ciudadano debe
/// aceptar el aviso de tratamiento de datos (ConsentAccepted) para poder
/// iniciar.
/// </summary>
public sealed record StartConversationRequest(
    string? DisplayName,
    string? Topic = null,
    bool ConsentAccepted = false,
    string? ConsentVersion = null);
