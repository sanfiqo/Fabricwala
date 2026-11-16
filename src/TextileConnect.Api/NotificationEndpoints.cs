using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Notifications;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed record NotificationResponse(long Id, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAt)
{
    public static NotificationResponse From(Notification x) => new(x.Id, x.Title, x.Message, x.Link, x.IsRead, x.CreatedAtUtc);
}

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").RequireAuthorization();
        static string UserOf(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        group.MapGet("", async (ClaimsPrincipal principal, MarketplaceDbContext db, bool? unread, int? page, int? pageSize, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20; var userId = UserOf(principal);
            if (number is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            var query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
            if (unread == true) query = query.Where(x => !x.IsRead);
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((number - 1) * size).Take(size).ToListAsync(ct);
            return Results.Ok(new PagedResponse<NotificationResponse>(rows.Select(NotificationResponse.From).ToList(), total, number, size));
        });
        group.MapGet("/unread-count", async (ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var userId = UserOf(principal);
            return Results.Ok(new { count = await db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, ct) });
        });
        group.MapPost("/{id:long}/read", async (long id, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var userId = UserOf(principal);
            var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (notification is null) return Results.NotFound();
            if (!notification.IsRead) { notification.IsRead = true; await db.SaveChangesAsync(ct); }
            return Results.Ok(NotificationResponse.From(notification));
        });
        group.MapPost("/read-all", async (ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var userId = UserOf(principal);
            var updated = 0;
            while (true)
            {
                var batch = await db.Notifications.Where(x => x.UserId == userId && !x.IsRead).OrderBy(x => x.Id).Take(500).ToListAsync(ct);
                if (batch.Count == 0) break;
                foreach (var item in batch) item.IsRead = true;
                await db.SaveChangesAsync(ct);
                updated += batch.Count;
            }
            return Results.Ok(new { updated });
        });
    }
}
