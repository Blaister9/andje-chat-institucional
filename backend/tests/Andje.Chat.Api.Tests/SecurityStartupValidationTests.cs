using Andje.Chat.Api.Security;
using Andje.Chat.Api.Services;

namespace Andje.Chat.Api.Tests;

public class SecurityStartupValidationTests
{
    private static readonly string[] ValidOrigins = ["http://localhost:5173"];

    [Fact]
    public void Configuracion_local_valida_no_reporta_problemas()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: true,
            agentAccess: EnabledWithCode("codigo-fuerte-local"),
            corsOrigins: ValidOrigins,
            autoMigrate: true,
            hasConnectionString: true);

        Assert.Empty(issues);
    }

    [Fact]
    public void Agent_access_habilitado_sin_codigo_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode(""),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: true);

        Assert.Contains(issues, i => i.Contains("DevelopmentAccessCode"));
    }

    [Fact]
    public void Cors_con_comodin_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ["*"],
            autoMigrate: false,
            hasConnectionString: true);

        Assert.Contains(issues, i => i.Contains("'*'"));
    }

    [Fact]
    public void Cors_vacio_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: [],
            autoMigrate: false,
            hasConnectionString: true);

        Assert.Contains(issues, i => i.Contains("Cors:AllowedOrigins"));
    }

    [Fact]
    public void Sin_cadena_de_conexion_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: false);

        Assert.Contains(issues, i => i.Contains("ConnectionStrings:ChatDb"));
    }

    [Fact]
    public void Automigrate_fuera_de_desarrollo_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ValidOrigins,
            autoMigrate: true,
            hasConnectionString: true);

        Assert.Contains(issues, i => i.Contains("AutoMigrate"));
    }

    [Fact]
    public void Codigo_local_conocido_fuera_de_desarrollo_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("andje-agent-local"),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: true);

        Assert.Contains(issues, i => i.Contains("local/dev conocido"));
    }

    [Fact]
    public void Codigo_local_conocido_en_desarrollo_o_pruebas_no_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: true,
            agentAccess: EnabledWithCode("andje-agent-local"),
            corsOrigins: ValidOrigins,
            autoMigrate: true,
            hasConnectionString: true);

        Assert.Empty(issues);
    }

    [Fact]
    public void Forwarded_headers_sin_proxy_conocido_fuera_de_desarrollo_es_problema()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: true,
            forwardedHeadersEnabled: true,
            hasKnownForwardedProxyOrNetwork: false);

        Assert.Contains(issues, i => i.Contains("ForwardedHeaders"));
    }

    [Fact]
    public void Forwarded_headers_sin_proxy_conocido_en_desarrollo_o_pruebas_no_es_fatal()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: true,
            agentAccess: EnabledWithCode("codigo-fuerte-local"),
            corsOrigins: ValidOrigins,
            autoMigrate: true,
            hasConnectionString: true,
            forwardedHeadersEnabled: true,
            hasKnownForwardedProxyOrNetwork: false);

        Assert.Empty(issues);
    }

    [Fact]
    public void Hsts_habilitado_en_desarrollo_o_pruebas_es_advertencia()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: true,
            agentAccess: EnabledWithCode("codigo-fuerte-local"),
            corsOrigins: ValidOrigins,
            autoMigrate: true,
            hasConnectionString: true,
            useHsts: true);

        Assert.Contains(issues, i => i.Contains("Https:UseHsts"));
    }

    [Fact]
    public void Require_https_explicito_fuera_de_desarrollo_es_permitido()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: true,
            requireHttps: true);

        Assert.Empty(issues);
    }

    [Fact]
    public void Forwarded_headers_con_proxy_conocido_fuera_de_desarrollo_es_permitido()
    {
        var issues = SecurityStartupValidation.Collect(
            isDevelopmentOrTest: false,
            agentAccess: EnabledWithCode("codigo-fuerte"),
            corsOrigins: ValidOrigins,
            autoMigrate: false,
            hasConnectionString: true,
            forwardedHeadersEnabled: true,
            hasKnownForwardedProxyOrNetwork: true);

        Assert.Empty(issues);
    }

    private static AgentAccessOptions EnabledWithCode(string code) => new()
    {
        Enabled = true,
        DevelopmentAccessCode = code,
        SessionMinutes = 120,
    };
}
