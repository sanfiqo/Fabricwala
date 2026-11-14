using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed record SellerMonthlySales(string Month, decimal Sales, int Orders);
public sealed record SellerLowStockItem(long ProductId, string Title, string Sku, string? Size, string? Colour, int StockQty);
public sealed record SellerLowStock(int Threshold, int Count, IReadOnlyList<SellerLowStockItem> Items);
public sealed record SellerBestSeller(long ProductId, string Title, string Slug, int Quantity, decimal Revenue);

/// <summary>Sales = goods value (before GST and freight) of paid orders that were not rejected or cancelled, by the month they were placed.</summary>
public sealed record SellerDashboardResponse(int NewOrders, int ToPack, decimal SalesThisMonth, decimal NextPayoutAmount, DateTime? NextPayoutDate, decimal OnHoldAmount,
    IReadOnlyList<SellerMonthlySales> MonthlySales, SellerLowStock LowStock, IReadOnlyList<SellerBestSeller> BestSellers);

public static class SellerDashboardEndpoints
{
    public const int LowStockThreshold = 5;

    public static void MapSellerDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seller/dashboard", async (ICurrentBusiness current, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var sellerId = current.RequireBusinessId();
            var now = clock.GetUtcNow().UtcDateTime;
            var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstMonth = thisMonth.AddMonths(-5);
            var paid = db.SellerOrders.AsNoTracking().Where(x => x.ManufacturerBusinessId == sellerId && x.Order!.PaymentStatus != "Pending" && x.Order.PaymentStatus != "Cancelled");
            var counted = paid.Where(x => x.Status != "Rejected" && x.Status != "Cancelled");

            var grouped = await counted.Where(x => x.PlacedAtUtc >= firstMonth)
                .GroupBy(x => new { x.PlacedAtUtc.Year, x.PlacedAtUtc.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Sales = g.Sum(x => x.SubTotal), Orders = g.Count() }).ToListAsync(ct);
            var chart = Enumerable.Range(0, 6).Select(i => firstMonth.AddMonths(i)).Select(month =>
            {
                var row = grouped.FirstOrDefault(g => g.Year == month.Year && g.Month == month.Month);
                return new SellerMonthlySales($"{month:yyyy-MM}", row?.Sales ?? 0m, row?.Orders ?? 0);
            }).ToList();

            var lowQuery = db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.StockQty <= LowStockThreshold)
                .Join(db.Products.Where(p => p.SellerBusinessId == sellerId && !p.IsDeleted && p.Status != ProductStatus.Draft), v => v.ProductId, p => p.Id, (v, p) => new { v, p });
            var lowItems = await lowQuery.OrderBy(x => x.v.StockQty).ThenBy(x => x.v.Id).Take(10)
                .Select(x => new SellerLowStockItem(x.p.Id, x.p.Title, x.v.Sku, x.v.Size, x.v.Colour, x.v.StockQty)).ToListAsync(ct);

            var best = await db.OrderItems.AsNoTracking().Where(i => counted.Select(s => s.Id).Contains(i.SellerOrderId))
                .GroupBy(i => i.ProductId).Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Quantity * i.UnitPrice) })
                .OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId).Take(5).ToListAsync(ct);
            var bestIds = best.Select(x => x.ProductId).ToArray();
            var products = await db.Products.AsNoTracking().Where(p => bestIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var payouts = await new SellerPayoutReader(db, clock).SummaryAsync(sellerId, null, 1, 1, ct);
            return Results.Ok(new SellerDashboardResponse(
                await paid.CountAsync(x => x.Status == "Pending", ct), await paid.CountAsync(x => x.Status == "Accepted", ct),
                chart[^1].Sales, payouts.NextPayoutAmount, payouts.NextPayoutDate, payouts.OnHoldAmount, chart,
                new SellerLowStock(LowStockThreshold, await lowQuery.CountAsync(ct), lowItems),
                best.Select(x => new SellerBestSeller(x.ProductId, products.GetValueOrDefault(x.ProductId)?.Title ?? string.Empty, products.GetValueOrDefault(x.ProductId)?.Slug ?? string.Empty, x.Quantity, x.Revenue)).ToList()));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Manufacturer"));
    }
}
