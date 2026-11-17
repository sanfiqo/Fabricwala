using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace TextileConnect.Api;

/// <summary>
/// Rate limits. Every limit is counted **per client address**: one shop typing fast, or one attacker, uses up only their own allowance.
/// (They used to be one counter for the whole site, so 60 searches a minute from everybody together would have blocked every shopper.)
/// Behind a proxy or load balancer the address is the real client only when Proxy:KnownProxies (or Proxy:KnownNetworks) is set, see ForwardedHeadersSetup.
/// </summary>
public static class ApiRateLimiting
{
    /// <summary>Sign in: one-time code requests and checks. The code request has its own, stricter, limits per number and per address.</summary>
    public const int AuthPerMinute = 30;
    /// <summary>Product list, search and filter counts.</summary>
    public const int SearchPerMinute = 120;
    /// <summary>Everything else together, as a backstop against a client that hammers the API.</summary>
    public const int AnyRequestPerMinute = 600;

    /// <summary>The counting key of a request: its address. An IPv6 address counts as its /64 network, so changing the last digits does not give a new allowance.</summary>
    public static string ClientKey(HttpContext context) => ClientKey(context.Connection.RemoteIpAddress);

    public static string ClientKey(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes).ToString() + "/64";
    }

    private static RateLimitPartition<string> PerClient(HttpContext context, string name, int permits)
        => RateLimitPartition.GetFixedWindowLimiter($"{name}:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });

    /// <summary>The "auth" and "search" limits and the overall backstop. A rejected call gets 429 and a Retry-After header in seconds.</summary>
    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        options.AddPolicy("auth", context => PerClient(context, "auth", AuthPerMinute));
        options.AddPolicy("search", context => PerClient(context, "search", SearchPerMinute));
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/api/v1/health") ? RateLimitPartition.GetNoLimiter("health") : PerClient(context, "any", AnyRequestPerMinute));
    });
}
