using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Andje.Chat.Api.Security;

public sealed class AndjeForwardedHeadersOptions
{
    public bool Enabled { get; set; }
    public int ForwardLimit { get; set; } = 1;
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}

public sealed class AndjeHttpsOptions
{
    public bool RequireHttps { get; set; }
    public bool UseHsts { get; set; }
}

public static class ForwardedHeadersConfiguration
{
    public static AndjeForwardedHeadersOptions GetForwardedHeadersOptions(IConfiguration configuration)
    {
        ApplyEnvironmentAliases(configuration);
        return configuration.GetSection("ForwardedHeaders").Get<AndjeForwardedHeadersOptions>()
            ?? new AndjeForwardedHeadersOptions();
    }

    public static ForwardedHeadersOptions ToMiddlewareOptions(AndjeForwardedHeadersOptions options)
    {
        var middlewareOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost,
            ForwardLimit = Math.Max(1, options.ForwardLimit),
        };

        foreach (var proxy in options.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                middlewareOptions.KnownProxies.Add(address);
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            var parsed = ParseNetwork(network);
            if (parsed is not null)
            {
                middlewareOptions.KnownNetworks.Add(parsed);
            }
        }

        return middlewareOptions;
    }

    public static bool HasKnownProxyOrNetwork(AndjeForwardedHeadersOptions options) =>
        options.KnownProxies.Any(proxy => IPAddress.TryParse(proxy, out _)) ||
        options.KnownNetworks.Any(network => ParseNetwork(network) is not null);

    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork? ParseNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var prefix) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            return null;
        }

        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
    }

    private static void ApplyEnvironmentAliases(IConfiguration configuration)
    {
        SetIfPresent(configuration, "ANDJE_FORWARDED_HEADERS_ENABLED", "ForwardedHeaders:Enabled");
        SetIfPresent(configuration, "ANDJE_FORWARDED_HEADERS_FORWARD_LIMIT", "ForwardedHeaders:ForwardLimit");
        SetListIfPresent(configuration, "ANDJE_FORWARDED_HEADERS_KNOWN_PROXIES", "ForwardedHeaders:KnownProxies");
        SetListIfPresent(configuration, "ANDJE_FORWARDED_HEADERS_KNOWN_NETWORKS", "ForwardedHeaders:KnownNetworks");
    }

    private static void SetIfPresent(IConfiguration configuration, string environmentKey, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            configuration[configurationKey] = value;
        }
    }

    private static void SetListIfPresent(IConfiguration configuration, string environmentKey, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var entries = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < entries.Length; i++)
        {
            configuration[$"{configurationKey}:{i}"] = entries[i];
        }
    }
}
