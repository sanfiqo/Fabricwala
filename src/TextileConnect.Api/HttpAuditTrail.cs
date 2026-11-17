using System.Security.Claims;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Takes the user and the IP address from the current request. Without a request (background jobs) the user is "system" and there is no IP.</summary>
public sealed class HttpAuditTrail(MarketplaceDbContext db, IHttpContextAccessor accessor, TimeProvider clock) : IAuditTrail
{
    public void Record(string action, string entityName, object? entityId = null, string? details = null)
    {
        var http = accessor.HttpContext;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = http is null ? "system" : http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
            Action = Trim(action, 64)!,
            EntityName = Trim(entityName, 200)!,
            EntityId = Trim(entityId?.ToString(), 64),
            IpAddress = Trim(http?.Connection.RemoteIpAddress?.ToString(), 45),
            Details = Trim(details, 1000),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
    }

    private static string? Trim(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
