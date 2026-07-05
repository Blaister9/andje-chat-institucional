using Andje.Chat.Api.Services;

namespace Andje.Chat.Api.Security;

/// <summary>
/// Detecta configuraciones inseguras al arranque. En entornos que no son de
/// desarrollo estos hallazgos son fatales (la app no arranca); en desarrollo
/// se emiten como advertencias para no romper el flujo local.
///
/// Nunca incluye valores sensibles (codigo de acceso, cadena de conexion) en
/// los mensajes: solo describe el problema y la clave de configuracion.
/// </summary>
public static class SecurityStartupValidation
{
    // Valores locales/dev conocidos que no deben usarse fuera de desarrollo.
    private static readonly string[] InsecureDefaultCodes =
    [
        "andje-agent-local",
        "change-me-local-only",
        "test-agent-code",
    ];

    public static IReadOnlyList<string> Collect(
        bool isDevelopment,
        AgentAccessOptions agentAccess,
        IReadOnlyList<string> corsOrigins,
        bool autoMigrate,
        bool hasConnectionString)
    {
        var issues = new List<string>();

        if (agentAccess.Enabled && string.IsNullOrWhiteSpace(agentAccess.DevelopmentAccessCode))
        {
            issues.Add("AgentAccess:Enabled es true pero AgentAccess:DevelopmentAccessCode esta vacio.");
        }

        if (corsOrigins.Count == 0)
        {
            issues.Add("Cors:AllowedOrigins esta vacio; la consola y el widget no podran conectarse.");
        }

        if (corsOrigins.Any(origin => origin == "*"))
        {
            issues.Add("Cors:AllowedOrigins no puede contener '*' porque se permiten credenciales.");
        }

        if (!hasConnectionString)
        {
            issues.Add("ConnectionStrings:ChatDb no esta configurada.");
        }

        if (!isDevelopment && autoMigrate)
        {
            issues.Add("Database:AutoMigrate deberia estar deshabilitado fuera de desarrollo.");
        }

        if (!isDevelopment &&
            agentAccess.Enabled &&
            InsecureDefaultCodes.Contains(agentAccess.DevelopmentAccessCode, StringComparer.Ordinal))
        {
            issues.Add("AgentAccess:DevelopmentAccessCode usa un valor local/dev conocido fuera de desarrollo.");
        }

        return issues;
    }
}
