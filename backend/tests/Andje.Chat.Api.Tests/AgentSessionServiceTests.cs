using Andje.Chat.Api.Contracts;
using Andje.Chat.Api.Services;
using Microsoft.Extensions.Options;

namespace Andje.Chat.Api.Tests;

public class AgentSessionServiceTests
{
    [Fact]
    public void Crear_sesion_con_codigo_valido_devuelve_token()
    {
        var service = CreateService();

        var response = service.CreateSession(
            new CreateAgentSessionRequest("Agente QA", "test-agent-code"));

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal("Agente QA", response.AgentDisplayName);
        Assert.True(response.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(service.ValidateToken(response.AccessToken).IsValid);
    }

    [Fact]
    public void Codigo_invalido_rechaza_sesion()
    {
        var service = CreateService();

        Assert.Throws<AgentSessionRejectedException>(() =>
            service.CreateSession(new CreateAgentSessionRequest("Agente QA", "incorrecto")));
    }

    [Fact]
    public void Nombre_vacio_usa_agente_local()
    {
        var service = CreateService();

        var response = service.CreateSession(
            new CreateAgentSessionRequest("   ", "test-agent-code"));

        Assert.Equal("Agente local", response.AgentDisplayName);
    }

    [Fact]
    public void Token_invalido_no_valida()
    {
        var service = CreateService();

        var validation = service.ValidateToken("token-invalido");

        Assert.True(validation.HasToken);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Token_expirado_no_valida()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(clock, sessionMinutes: 1);
        var response = service.CreateSession(
            new CreateAgentSessionRequest("Agente QA", "test-agent-code"));

        clock.Advance(TimeSpan.FromMinutes(2));

        var validation = service.ValidateToken(response.AccessToken);
        Assert.True(validation.HasToken);
        Assert.False(validation.IsValid);
    }

    private static AgentSessionService CreateService(
        TimeProvider? timeProvider = null,
        int sessionMinutes = 120) => new(
            Options.Create(new AgentAccessOptions
            {
                Enabled = true,
                DevelopmentAccessCode = "test-agent-code",
                SessionMinutes = sessionMinutes,
            }),
            timeProvider ?? TimeProvider.System);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }
}
