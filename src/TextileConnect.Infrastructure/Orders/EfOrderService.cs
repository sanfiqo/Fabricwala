using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Notifications;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfOrderService(MarketplaceDbContext db) : IOrderService
{
    public async Task<IReadOnlyList<Order>> ListForRetailerAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.Orders.Where(x => x.RetailerBusinessId == businessId).OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
    public async Task<Order?> GetForRetailerAsync(long businessId, long orderId, CancellationToken cancellationToken = default)
        => await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId && x.RetailerBusinessId == businessId, cancellationToken);
    public async Task<Order?> ConfirmDeliveryAsync(long businessId, long orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetForRetailerAsync(businessId, orderId, cancellationToken);
        if (order is null) return null;
        var sellerOrders = await db.SellerOrders.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        var shipped = sellerOrders.Where(x => x.Status == "Dispatched").ToList();
        if (shipped.Count == 0)
        {
            if (sellerOrders.Count > 0 && sellerOrders.All(x => x.Status == "Delivered")) return order; // already confirmed: nothing changes
            throw new RuleViolationException("order_not_shipped", "This order has not been shipped yet.");
        }
        var now = DateTime.UtcNow;
        var shippedIds = shipped.Select(x => x.Id).ToArray();
        foreach (var sellerOrder in shipped)
        {
            sellerOrder.Status = "Delivered";
            sellerOrder.DeliveredAt = now;
            db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = "Delivered", Note = "Delivery confirmed by the retailer.", ChangedAtUtc = now });
        }
        foreach (var shipment in await db.Shipments.Where(x => shippedIds.Contains(x.SellerOrderId)).ToListAsync(cancellationToken))
        { shipment.Status = "Delivered"; shipment.DeliveredAt = now; }
        var sellerIds = shipped.Select(x => x.ManufacturerBusinessId).Distinct().ToArray();
        var sellerUsers = await db.BusinessUsers.AsNoTracking().Where(x => sellerIds.Contains(x.BusinessId)).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        db.Notifications.AddRange(sellerUsers.Select(userId => new Notification
        { UserId = userId, Title = "Delivery confirmed", Message = $"The retailer confirmed delivery of order {order.OrderNumber}.", Link = "/seller/orders", CreatedAtUtc = now }));
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<IReadOnlyList<SellerOrder>> ListForSellerAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.SellerOrders.Where(x => x.ManufacturerBusinessId == businessId).OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
    public async Task<SellerOrder?> GetForSellerAsync(long businessId, long sellerOrderId, CancellationToken cancellationToken = default)
        => await db.SellerOrders.SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken);

    public async Task<SellerOrder?> UpdateSellerOrderAsync(long businessId, long sellerOrderId, string status, string? reason, string? lrNumber, CancellationToken cancellationToken = default)
    {
        var order = await db.SellerOrders.SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken);
        if (order is null) return null;
        var parent = await db.Orders.SingleAsync(x => x.Id == order.OrderId, cancellationToken);
        if (status == "Accepted" && !string.Equals(parent.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seller orders can only be accepted after payment.");
        order.Status = status;
        order.RejectReason = reason;
        db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = order.Id, Status = status, Note = reason });
        if (status == "Accepted") order.AcceptedAt = DateTime.UtcNow;
        if (status == "Dispatched")
        {
            order.DispatchedAt = DateTime.UtcNow;
            var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == order.Id, cancellationToken);
            if (shipment is null) db.Shipments.Add(new Shipment { SellerOrderId = order.Id, LrNumber = lrNumber, DispatchedAt = order.DispatchedAt });
            else { shipment.LrNumber = lrNumber; shipment.DispatchedAt = order.DispatchedAt; shipment.Status = "Dispatched"; }
        }
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<Shipment?> UpdateShipmentAsync(long businessId, long sellerOrderId, string status, CancellationToken cancellationToken = default)
    {
        if (!await db.SellerOrders.AnyAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken)) return null;
        var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrderId, cancellationToken);
        if (shipment is null) return null;
        shipment.Status = status;
        if (status == "Delivered") shipment.DeliveredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return shipment;
    }
}
