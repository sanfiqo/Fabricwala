using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>One row of the seller's orders list. Tab is new, to_pack, shipped or done. Items are the order lines (same shape as in the order detail).</summary>
public sealed record SellerOrderSummaryResponse(long Id, string OrderNumber, DateTime PlacedAt, string Status, string Tab, string RetailerName, string DeliveryCity,
    int ItemCount, int TotalQuantity, string FirstItemTitle, decimal OrderValue, decimal PlatformFee, decimal YouGet, DateTime? AcceptDeadline, IReadOnlyList<OrderItemResponse> Items);

/// <summary>Read side of the seller's orders. Sellers only see orders the retailer has paid for, and only their own.</summary>
public sealed class SellerOrderReader(MarketplaceDbContext db)
{
    public static bool IsValidTab(string? tab) => tab is null or "all" or "new" or "to_pack" or "shipped" or "done";

    public static string TabOf(string status) => status switch
    {
        "Pending" => "new", "Accepted" => "to_pack", "Dispatched" => "shipped", _ => "done"
    };

    private IQueryable<SellerOrder> Visible(long sellerId)
        => db.SellerOrders.AsNoTracking().Where(x => x.ManufacturerBusinessId == sellerId
            && (x.Order!.PaymentStatus == "Paid" || x.Order.PaymentStatus == "Refunded" || x.Order.PaymentStatus == "PartiallyRefunded"));

    public async Task<PagedResponse<SellerOrderSummaryResponse>> ListAsync(long sellerId, string? tab, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible(sellerId);
        query = tab switch
        {
            "new" => query.Where(x => x.Status == "Pending"),
            "to_pack" => query.Where(x => x.Status == "Accepted"),
            "shipped" => query.Where(x => x.Status == "Dispatched"),
            "done" => query.Where(x => x.Status != "Pending" && x.Status != "Accepted" && x.Status != "Dispatched"),
            _ => query
        };
        var total = await query.CountAsync(ct);
        // New orders: the ones closest to the acceptance deadline first. Everything else: newest first.
        var ordered = tab == "new"
            ? query.OrderBy(x => x.AcceptDeadlineUtc == null).ThenBy(x => x.AcceptDeadlineUtc).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.PlacedAtUtc).ThenByDescending(x => x.Id);
        var rows = await ordered.Include(x => x.Order).Include(x => x.Items).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var retailerIds = rows.Select(x => x.Order!.RetailerBusinessId).Distinct().ToArray();
        var names = await db.Businesses.AsNoTracking().Where(x => retailerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var items = rows.Select(x =>
        {
            var lines = x.Items.OrderBy(i => i.Id).ToList();
            return new SellerOrderSummaryResponse(x.Id, x.Order!.OrderNumber, x.PlacedAtUtc, Display(x.Status), TabOf(x.Status), names.GetValueOrDefault(x.Order.RetailerBusinessId) ?? string.Empty,
                x.Order.DeliveryCity, lines.Count, lines.Sum(i => i.Quantity), lines.FirstOrDefault()?.Title ?? string.Empty, x.Total, x.CommissionAmount, x.Total - x.CommissionAmount,
                x.Status == "Pending" ? x.AcceptDeadlineUtc : null, lines.Select(ApiResponseMappings.OrderLine).ToList());
        }).ToList();
        return new PagedResponse<SellerOrderSummaryResponse>(items, total, page, pageSize);
    }

    public async Task<SellerOrderResponse?> DetailAsync(long sellerId, long sellerOrderId, CancellationToken ct)
    {
        var order = await Visible(sellerId).Include(x => x.Order).Include(x => x.Items).Include(x => x.History).SingleOrDefaultAsync(x => x.Id == sellerOrderId, ct);
        if (order is null) return null;
        var names = await db.Businesses.AsNoTracking().Where(x => x.Id == sellerId || x.Id == order.Order!.RetailerBusinessId).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var shipment = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.SellerOrderId == order.Id, ct);
        return SellerOrderResponse.From(order) with
        {
            ManufacturerName = names.GetValueOrDefault(sellerId) ?? string.Empty,
            RetailerName = names.GetValueOrDefault(order.Order!.RetailerBusinessId) ?? string.Empty,
            DeliveryCity = order.Order.DeliveryCity,
            Shipment = shipment is null ? null : new ShipmentResponse(shipment.CarrierName, shipment.LrNumber, shipment.DispatchedAt),
        };
    }

    public static string Display(string status) => status == "Pending" ? "PendingAcceptance" : status;
}
