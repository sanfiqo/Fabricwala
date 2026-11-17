using System.Security.Claims;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>
/// Records every successful change (POST, PUT, PATCH, DELETE) under /api/v1/admin: who, which route, which id, when and from which IP.
/// New admin endpoints are covered without any extra code. The request body is never stored.
/// </summary>
public sealed class AdminAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, MarketplaceDbContext db, TimeProvider clock)
    {
        await next(context);
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method)) return;
        if (!context.Request.Path.StartsWithSegments("/api/v1/admin") || context.Response.StatusCode is < 200 or >= 300) return;
        if (context.User.Identity?.IsAuthenticated != true) return;

        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty;
        var shortRoute = pattern.StartsWith("/api/v1/admin/", StringComparison.Ordinal) ? pattern["/api/v1/admin/".Length..] : pattern;
        var segments = shortRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = context.Request.RouteValues.Values.Select(x => x?.ToString()).FirstOrDefault(x => long.TryParse(x, out _));
        var action = $"{context.Request.Method} {shortRoute}";
        db.AuditLogs.Add(new AuditLog
        {
            UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action.Length <= 64 ? action : action[..64],
            EntityName = segments.FirstOrDefault() ?? "admin",
            EntityId = id,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(context.RequestAborted);
    }
}
