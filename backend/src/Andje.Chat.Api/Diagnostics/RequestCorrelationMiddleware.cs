namespace Andje.Chat.Api.Diagnostics;

public static class RequestCorrelationMiddleware
{
    private const string RequestIdHeader = "X-Request-ID";

    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var requestId = context.Request.Headers.TryGetValue(RequestIdHeader, out var incoming)
                && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : context.TraceIdentifier;

            context.TraceIdentifier = requestId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestIdHeader] = requestId;
                return Task.CompletedTask;
            });

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RequestCorrelation");
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
            }))
            {
                await next();
            }
        });
    }
}
