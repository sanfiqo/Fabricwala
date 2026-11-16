using System.Net;

namespace TextileConnect.Api;

/// <summary>
/// Which browser origins may call the API with cookies. Outside Development only the origins listed in configuration
/// (Cors:FrontendOrigin or Cors:FrontendOrigins) are allowed. In Development the Vite dev server on port 5173 is also allowed from
/// localhost, 127.0.0.1 and private network addresses, so a phone on the same Wi-Fi can use it.
/// </summary>
public static class FrontendCors
{
    public const string PolicyName = "frontend";
    public const int DevPort = 5173;

    public static void AddFrontendCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = Configured(configuration);
        var development = environment.IsDevelopment();
        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .SetIsOriginAllowed(origin => IsAllowed(origin, development, configured))
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
    }

    public static IReadOnlyList<string> Configured(IConfiguration configuration)
    {
        var list = configuration.GetSection("Cors:FrontendOrigins").Get<string[]>() ?? [];
        return list.Append(configuration["Cors:FrontendOrigin"]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim().TrimEnd('/')).ToList();
    }

    public static bool IsAllowed(string? origin, bool development, IReadOnlyCollection<string> configured)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.PathAndQuery != "/" || uri.Scheme is not ("http" or "https")) return false;
        var normalized = $"{uri.Scheme}://{uri.Authority}";
        if (configured.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase))) return true;
        if (!development || uri.Port != DevPort) return false;
        if (uri.Host is "localhost" or "127.0.0.1") return true;
        return uri.Scheme == "http" && IPAddress.TryParse(uri.Host, out var ip) && IsPrivateIPv4(ip);
    }

    private static bool IsPrivateIPv4(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
