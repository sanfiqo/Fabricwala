using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Orders;
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
        foreach (var sellerOrder in sellerOrders) sellerOrder.Status = "Delivered";
        var shipments = await db.Shipments.Where(x => sellerOrders.Select(s => s.Id).Contains(x.SellerOrderId)).ToListAsync(cancellationToken);
        foreach (var shipment in shipments) { shipment.Status = "Delivered"; shipment.DeliveredAt = DateTime.UtcNow; }
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
