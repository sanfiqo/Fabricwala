using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Read side of the retailer's orders. Every query is filtered by the retailer's business id.</summary>
public sealed class RetailerOrderReader(MarketplaceDbContext db)
{
    public static bool IsValidGroup(string? group) => group is null or "all" or OrderStatusRules.Active or OrderStatusRules.Delivered or OrderStatusRules.Problems;

    public async Task<PagedResponse<RetailerOrderSummaryResponse>> ListAsync(long retailerId, string? group, int page, int pageSize, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.RetailerBusinessId == retailerId);
        // problems: a seller order was rejected/cancelled or a claim is open. delivered: paid and every seller order delivered (and no problem). active: the rest.
        orders = group switch
        {
            OrderStatusRules.Problems => orders.Where(o => o.SellerOrders.Any(s => s.Status == "Rejected" || s.Status == "Cancelled")
                || db.ReturnClaims.Any(c => c.ResolvedAtUtc == null && c.SellerOrder!.OrderId == o.Id)),
            OrderStatusRules.Delivered => orders.Where(o => !o.SellerOrders.Any(s => s.Status == "Rejected" || s.Status == "Cancelled")
                && !db.ReturnClaims.Any(c => c.ResolvedAtUtc == null && c.SellerOrder!.OrderId == o.Id)
                && o.PaymentStatus == "Paid" && o.SellerOrders.Any() && o.SellerOrders.All(s => s.Status == "Delivered")),
            OrderStatusRules.Active => orders.Where(o => !o.SellerOrders.Any(s => s.Status == "Rejected" || s.Status == "Cancelled")
                && !db.ReturnClaims.Any(c => c.ResolvedAtUtc == null && c.SellerOrder!.OrderId == o.Id)
                && !(o.PaymentStatus == "Paid" && o.SellerOrders.Any() && o.SellerOrders.All(s => s.Status == "Delivered"))),
            _ => orders
        };
        var total = await orders.CountAsync(ct);
        var rows = await orders.OrderByDescending(o => o.PlacedAtUtc).ThenByDescending(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = rows.Select(o => o.Id).ToArray();
        var sellerOrders = await db.SellerOrders.AsNoTracking().Include(x => x.Items).Where(x => ids.Contains(x.OrderId)).ToListAsync(ct);
        var sellerIds = sellerOrders.Select(x => x.ManufacturerBusinessId).Distinct().ToArray();
        var names = await db.Businesses.AsNoTracking().Where(x => sellerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var sellerOrderIds = sellerOrders.Select(x => x.Id).ToArray();
        var claimOrders = (await db.ReturnClaims.AsNoTracking().Where(c => c.ResolvedAtUtc == null && sellerOrderIds.Contains(c.SellerOrderId)).Select(c => c.SellerOrderId).ToListAsync(ct)).ToHashSet();
        var items = rows.Select(order =>
        {
            var mine = sellerOrders.Where(x => x.OrderId == order.Id).OrderBy(x => x.Id).ToList();
            var (status, orderGroup) = OrderStatusRules.Derive(order.PaymentStatus, mine.Select(x => x.Status).ToList(), mine.Any(x => claimOrders.Contains(x.Id)));
            var lines = mine.SelectMany(x => x.Items).OrderBy(x => x.Id).ToList();
            return new RetailerOrderSummaryResponse(order.Id, order.OrderNumber, order.PlacedAtUtc, order.GrandTotal, order.PaymentStatus, status, orderGroup,
                lines.Count, lines.Sum(x => x.Quantity), lines.FirstOrDefault()?.Title ?? string.Empty,
                mine.Select(x => names.GetValueOrDefault(x.ManufacturerBusinessId) ?? string.Empty).ToList());
        }).ToList();
        return new PagedResponse<RetailerOrderSummaryResponse>(items, total, page, pageSize);
    }

    public async Task<OrderResponse?> DetailAsync(long retailerId, long orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.SellerOrders).ThenInclude(x => x.Items).Include(x => x.SellerOrders).ThenInclude(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.RetailerBusinessId == retailerId, ct);
        if (order is null) return null;
        var sellerIds = order.SellerOrders.Select(x => x.ManufacturerBusinessId).Distinct().ToArray();
        var names = await db.Businesses.AsNoTracking().Where(x => sellerIds.Contains(x.Id) || x.Id == retailerId).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var sellerOrderIds = order.SellerOrders.Select(x => x.Id).ToArray();
        var shipments = await db.Shipments.AsNoTracking().Where(x => sellerOrderIds.Contains(x.SellerOrderId)).ToDictionaryAsync(x => x.SellerOrderId, ct);
        var openClaims = await db.ReturnClaims.AsNoTracking().AnyAsync(c => c.ResolvedAtUtc == null && sellerOrderIds.Contains(c.SellerOrderId), ct);
        var (status, group) = OrderStatusRules.Derive(order.PaymentStatus, order.SellerOrders.Select(x => x.Status).ToList(), openClaims);
        var response = OrderResponse.From(order);
        return response with
        {
            Status = status, Group = group,
            SellerOrders = response.SellerOrders.Select(x => x with
            {
                ManufacturerName = names.GetValueOrDefault(order.SellerOrders.Single(s => s.Id == x.Id).ManufacturerBusinessId) ?? string.Empty,
                RetailerName = names.GetValueOrDefault(retailerId) ?? string.Empty,
                Shipment = shipments.TryGetValue(x.Id, out var shipment) ? new ShipmentResponse(shipment.CarrierName, shipment.LrNumber, shipment.DispatchedAt) : null,
            }).ToList()
        };
    }
}
