using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Andje.Chat.Api.Tests;

public class SmokeTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Health_responde_200_con_estado_healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hub_de_chat_acepta_conexiones_y_responde_ping()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/chat"), options =>
            {
                // TestServer no soporta WebSockets; long polling basta para el smoke test.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        var respuesta = await connection.InvokeAsync<string>("Ping");

        Assert.Equal("pong", respuesta);
    }
}
