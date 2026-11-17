using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace TextileConnect.Api;

/// <summary>
/// Lets the app see the real client address and scheme when it runs behind a reverse proxy or load balancer (X-Forwarded-For and X-Forwarded-Proto).
/// The headers are trusted **only** from the proxies listed in configuration: otherwise anybody could write any address in the header and dodge
/// the rate limits, the one-time code limits and the audit log. With no list the headers are ignored.
///   Proxy:KnownProxies   = ["10.0.0.5"]            single proxy addresses
///   Proxy:KnownNetworks  = ["10.0.0.0/24"]         or whole networks
/// </summary>
public static class ForwardedHeadersSetup
{
    public static ForwardedHeadersOptions? Options(IConfiguration configuration)
    {
        var proxies = configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("Proxy:KnownNetworks").Get<string[]>() ?? [];
        if (proxies.Length == 0 && networks.Length == 0) return null;

        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear(); // the defaults trust only loopback; the list below replaces them
        foreach (var proxy in proxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
        foreach (var network in networks)
        {
            var parts = network.Split('/');
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(parts[0]), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
        }
        return options;
    }
}
