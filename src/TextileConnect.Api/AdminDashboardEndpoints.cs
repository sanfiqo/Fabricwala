using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/dashboard", async (int? days, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var window = Math.Clamp(days ?? 30, 1, 365); var since = DateTime.UtcNow.AddDays(-window);
            var orders = db.Orders.Where(x => x.PaymentStatus == "Paid");
            var sellerOrders = db.SellerOrders.Join(orders, sellerOrder => sellerOrder.OrderId, order => order.Id, (sellerOrder, order) => sellerOrder);
            return Results.Ok(new { windowDays = window, gmv = await orders.SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0, orders = await orders.CountAsync(ct), commission = await sellerOrders.SumAsync(x => (decimal?)(x.SubTotal * x.CommissionRate), ct) ?? 0, newBusinesses = await db.Businesses.CountAsync(x => x.CreatedAtUtc >= since && !x.IsDeleted, ct), pendingApprovals = await db.Businesses.CountAsync(x => x.VerificationStatus == Domain.Businesses.VerificationStatus.Pending && !x.IsDeleted, ct) });
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
