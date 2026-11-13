using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").RequireAuthorization();
        group.MapGet("", async (ClaimsPrincipal principal, MarketplaceDbContext db, int? page, int? pageSize, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var size = Math.Clamp(pageSize ?? 20, 1, 100); var skip = Math.Max(0, (page ?? 1) - 1) * size;
            var items = await db.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Skip(skip).Take(size).ToListAsync(ct);
            return Results.Ok(new { items, page = Math.Max(1, page ?? 1), pageSize = size });
        });
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
            Results.Ok(new { count = await db.Notifications.CountAsync(x => x.UserId == (principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty) && !x.IsRead, ct) }));
        group.MapPost("/{id:long}/read", async (long id, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == (principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty), ct);
            if (notification is null) return Results.NotFound(); notification.IsRead = true; await db.SaveChangesAsync(ct); return Results.Ok(notification);
        });
    }
}
