using Andje.Chat.Api.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Andje.Chat.Api.Tests;

public class CitizenIntakeTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Iniciar_conversacion_sin_consentimiento_es_rechazado()
    {
        await using var visitor = CreateConnection();
        await visitor.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => visitor.InvokeAsync<ConversationDto>(
            "StartConversation",
            new StartConversationRequest("Ciudadano Demo Widget", "Seguimiento", ConsentAccepted: false)));
    }

    [Fact]
    public async Task Iniciar_conversacion_con_consentimiento_guarda_tema()
    {
        await using var visitor = CreateConnection();
        await visitor.StartAsync();

        var conversation = await visitor.InvokeAsync<ConversationDto>(
            "StartConversation",
            new StartConversationRequest(
                "Ciudadano Demo Widget", "Seguimiento", ConsentAccepted: true, ConsentVersion: "demo-v1"));

        Assert.Equal("Pending", conversation.Status);
        Assert.Equal("Seguimiento", conversation.Topic);
        Assert.Equal("Ciudadano Demo Widget", conversation.VisitorDisplayName);
    }

    private HubConnection CreateConnection() => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/chat"), options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        })
        .Build();
}
