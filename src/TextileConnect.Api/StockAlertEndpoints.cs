using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class StockAlertEndpoints
{
    public static void MapStockAlertEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/seller/inventory").RequireAuthorization("ApprovedBusiness");
        group.MapGet("/alerts", async (ICurrentBusiness current, MarketplaceDbContext db, int? threshold, CancellationToken ct) =>
        {
            var limit = Math.Clamp(threshold ?? 5, 0, 1000);
            var alerts = await db.ProductVariants.Join(db.Products, variant => variant.ProductId, product => product.Id, (variant, product) => new { variant, product }).Where(x => x.product.SellerBusinessId == current.RequireBusinessId() && x.variant.IsActive && x.variant.StockQty <= limit).Select(x => new { x.variant.Id, x.variant.ProductId, x.variant.Sku, x.variant.StockQty, alert = x.variant.StockQty == 0 ? "OutOfStock" : "LowStock" }).ToListAsync(ct);
            return Results.Ok(new { threshold = limit, alerts });
        });
        group.MapGet("/duplicates", async (ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var duplicates = await db.Products.Where(x => x.SellerBusinessId == current.RequireBusinessId() && !x.IsDeleted).GroupBy(x => x.Title.ToUpper()).Where(x => x.Count() > 1).Select(x => new { title = x.Key, productIds = x.Select(y => y.Id).ToList() }).ToListAsync(ct);
            return Results.Ok(duplicates);
        });
    }
}
