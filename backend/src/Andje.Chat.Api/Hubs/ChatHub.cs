using Microsoft.AspNetCore.SignalR;

namespace Andje.Chat.Api.Hubs;

/// <summary>
/// Hub de chat en tiempo real. En esta fase solo expone un método de
/// verificación de conectividad; los métodos de conversación (enviar mensaje,
/// unirse a una conversación, asignar agente) llegan en la fase de mensajería.
/// </summary>
public sealed class ChatHub : Hub
{
    public string Ping() => "pong";
}
