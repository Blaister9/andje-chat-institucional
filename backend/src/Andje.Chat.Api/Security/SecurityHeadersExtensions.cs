namespace Andje.Chat.Api.Security;

/// <summary>
/// Headers HTTP de seguridad aplicados a todas las respuestas. Son controles
/// defensivos basicos; no sustituyen HTTPS, CSP de despliegue estatico ni una
/// revision de seguridad institucional.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            // El backend expone solo endpoints de datos/tiempo real; no cachear.
            headers["Cache-Control"] = "no-store";

            await next();
        });
    }
}
