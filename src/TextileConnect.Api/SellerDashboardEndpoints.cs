using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class SellerDashboardEndpoints
{
    public static void MapSellerDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seller/dashboard", async (ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var businessId = current.RequireBusinessId(); var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var sellerOrders = db.SellerOrders.Where(x => x.ManufacturerBusinessId == businessId);
            var monthly = sellerOrders.Where(x => x.AcceptedAt >= monthStart);
            var topProducts = await db.OrderItems.Where(x => x.SellerOrder != null && x.SellerOrder.ManufacturerBusinessId == businessId).GroupBy(x => x.ProductId).Select(x => new { productId = x.Key, quantity = x.Sum(y => y.Quantity) }).OrderByDescending(x => x.quantity).Take(5).ToListAsync(ct);
            var lowStock = await db.ProductVariants.Join(db.Products, variant => variant.ProductId, product => product.Id, (variant, product) => new { variant, product }).Where(x => x.product.SellerBusinessId == businessId && x.variant.IsActive && x.variant.StockQty <= 5).Select(x => new { x.variant.Id, x.variant.ProductId, x.variant.Sku, x.variant.StockQty }).ToListAsync(ct);
            return Results.Ok(new { newOrders = await sellerOrders.CountAsync(x => x.Status == "Pending", ct), pendingDispatch = await sellerOrders.CountAsync(x => x.Status == "Accepted", ct), salesThisMonth = await monthly.SumAsync(x => (decimal?)x.SubTotal, ct) ?? 0, payoutDue = await db.Settlements.Where(x => x.ManufacturerBusinessId == businessId && x.Status == "Pending").SumAsync(x => (decimal?)x.Amount, ct) ?? 0, topProducts, lowStock });
        }).RequireAuthorization("ApprovedBusiness");
    }
}
