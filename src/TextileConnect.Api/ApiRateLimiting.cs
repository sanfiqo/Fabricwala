using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace TextileConnect.Api;

public static class ApiRateLimiting
{
    /// <summary>The "auth" and "search" fixed window limits. A rejected call gets 429 and, when the limiter knows it, a Retry-After header in seconds.</summary>
    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        };
        options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; });
        options.AddFixedWindowLimiter("search", limiter => { limiter.PermitLimit = 60; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; });
    });
}
