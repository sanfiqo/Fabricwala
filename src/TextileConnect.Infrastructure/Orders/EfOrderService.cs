using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Orders;
using TextileConnect.Application.Payments;
using TextileConnect.Domain.Notifications;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfOrderService(MarketplaceDbContext db, IPaymentGateway? gateway = null, TimeProvider? clock = null) : IOrderService, IOrderMaintenance
{
    private const int DefaultUnpaidExpiryMinutes = 60;
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

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
        var now = Now;
        var shippedIds = shipped.Select(x => x.Id).ToArray();
        foreach (var sellerOrder in shipped)
        {
            sellerOrder.Status = "Delivered";
            sellerOrder.DeliveredAt = now;
            db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = "Delivered", Note = "Delivery confirmed by the retailer.", ChangedAtUtc = now });
        }
        foreach (var shipment in await db.Shipments.Where(x => shippedIds.Contains(x.SellerOrderId)).ToListAsync(cancellationToken))
        { shipment.Status = "Delivered"; shipment.DeliveredAt = now; }
        await NotifyBusinessesAsync(shipped.Select(x => x.ManufacturerBusinessId), "Delivery confirmed", $"The retailer confirmed delivery of order {order.OrderNumber}.", "/seller/orders", now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<IReadOnlyList<SellerOrder>> ListForSellerAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.SellerOrders.Where(x => x.ManufacturerBusinessId == businessId).OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
    public async Task<SellerOrder?> GetForSellerAsync(long businessId, long sellerOrderId, CancellationToken cancellationToken = default)
        => await db.SellerOrders.SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken);

    public Task<SellerOrder?> UpdateSellerOrderAsync(long businessId, long sellerOrderId, string status, string? reason, string? lrNumber, CancellationToken cancellationToken = default)
        => UpdateSellerOrderAsync(businessId, sellerOrderId, status, reason, lrNumber, null, cancellationToken);

    public async Task<SellerOrder?> UpdateSellerOrderAsync(long businessId, long sellerOrderId, string status, string? reason, string? lrNumber, string? carrierName, CancellationToken cancellationToken = default)
    {
        var order = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken);
        if (order is null) return null;
        var parent = order.Order!;
        var now = Now;
        Refund? refund = null;
        switch (status)
        {
            case "Accepted":
                RequireState(order, parent, "Pending");
                if (order.AcceptDeadlineUtc is { } deadline && deadline <= now)
                    throw new RuleViolationException("accept_window_expired", "The time to accept this order has passed.");
                order.Status = "Accepted";
                order.AcceptedAt = now;
                db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = order.Id, Status = "Accepted", ChangedAtUtc = now });
                await NotifyBusinessesAsync([parent.RetailerBusinessId], "Order accepted", $"The seller accepted order {parent.OrderNumber}.", $"/orders/{parent.Id}", now, cancellationToken);
                break;
            case "Rejected":
                RequireState(order, parent, "Pending");
                var rejection = reason?.Trim();
                if (string.IsNullOrEmpty(rejection) || rejection.Length > 500) throw new ArgumentException("A reason of up to 500 characters is required.");
                refund = await CancelAsync(order, parent, "Rejected", rejection, "Order rejected", $"The seller could not accept order {parent.OrderNumber}. You will be refunded.", now, cancellationToken);
                break;
            case "Dispatched":
                RequireState(order, parent, "Accepted");
                var lr = lrNumber?.Trim();
                var carrier = carrierName?.Trim();
                if (string.IsNullOrEmpty(lr) || lr.Length > 60 || carrier?.Length > 80) throw new ArgumentException("An LR/tracking number of up to 60 characters is required.");
                order.Status = "Dispatched";
                order.DispatchedAt = now;
                var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == order.Id, cancellationToken);
                if (shipment is null) db.Shipments.Add(new Shipment { SellerOrderId = order.Id, LrNumber = lr, CarrierName = carrier, DispatchedAt = now });
                else { shipment.LrNumber = lr; shipment.CarrierName = carrier; shipment.DispatchedAt = now; shipment.Status = "Dispatched"; }
                db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = order.Id, Status = "Dispatched", Note = $"LR {lr}", ChangedAtUtc = now });
                await NotifyBusinessesAsync([parent.RetailerBusinessId], "Order shipped", $"Order {parent.OrderNumber} was shipped. LR number {lr}.", $"/orders/{parent.Id}", now, cancellationToken);
                break;
            default:
                throw new ArgumentException("Unsupported status. Use Accepted, Rejected or Dispatched.");
        }
        await db.SaveChangesAsync(cancellationToken);
        if (refund is not null) await ProcessRefundAsync(refund, cancellationToken);
        return order;
    }

    private static void RequireState(SellerOrder order, Order parent, string expected)
    {
        if (!string.Equals(parent.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new RuleViolationException("order_not_paid", "This order is not paid yet.");
        if (order.Status != expected)
            throw new RuleViolationException("invalid_order_transition", "This order cannot be changed in its current state.");
    }

    public async Task<Shipment?> UpdateShipmentAsync(long businessId, long sellerOrderId, string status, CancellationToken cancellationToken = default)
    {
        if (!await db.SellerOrders.AnyAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken)) return null;
        var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrderId, cancellationToken);
        if (shipment is null) return null;
        shipment.Status = status;
        if (status == "Delivered") shipment.DeliveredAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var now = Now;
        var cancelled = 0;
        var refunds = new List<Refund>();

        var late = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items)
            .Where(x => x.Status == "Pending" && x.Order!.PaymentStatus == "Paid" && x.AcceptDeadlineUtc != null && x.AcceptDeadlineUtc <= now)
            .OrderBy(x => x.Id).Take(200).ToListAsync(cancellationToken);
        foreach (var sellerOrder in late)
        {
            var refund = await CancelAsync(sellerOrder, sellerOrder.Order!, "Cancelled", "The seller did not accept in time.", "Order cancelled",
                $"Order {sellerOrder.Order!.OrderNumber} was cancelled because it was not accepted in time. You will be refunded.", now, cancellationToken);
            if (refund is not null) refunds.Add(refund);
            cancelled++;
        }

        var cutoff = now.AddMinutes(-await UnpaidExpiryMinutesAsync(cancellationToken));
        var unpaid = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items)
            .Where(x => x.Status == "Pending" && x.Order!.PaymentStatus == "Pending" && x.Order.PlacedAtUtc <= cutoff)
            .OrderBy(x => x.Id).Take(200).ToListAsync(cancellationToken);
        foreach (var sellerOrder in unpaid)
        {
            await CancelAsync(sellerOrder, sellerOrder.Order!, "Cancelled", "Payment was not completed in time.", "Order cancelled",
                $"Order {sellerOrder.Order!.OrderNumber} was cancelled because payment was not completed.", now, cancellationToken);
            cancelled++;
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var refund in refunds) await ProcessRefundAsync(refund, cancellationToken);
        await RetryRequestedRefundsAsync(cancellationToken);
        return cancelled;
    }

    /// <summary>Cancels a seller order: status, stock back on the shelf, full refund when paid, history and notifications. Caller saves.</summary>
    private async Task<Refund?> CancelAsync(SellerOrder sellerOrder, Order parent, string finalStatus, string reason, string title, string message, DateTime now, CancellationToken cancellationToken)
    {
        sellerOrder.Status = finalStatus;
        sellerOrder.RejectReason = reason;
        var variantIds = sellerOrder.Items.Where(x => x.VariantId.HasValue).Select(x => x.VariantId!.Value).ToArray();
        var variants = await db.ProductVariants.Where(x => variantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in sellerOrder.Items.Where(x => x.VariantId.HasValue))
            if (variants.TryGetValue(item.VariantId!.Value, out var variant)) variant.StockQty += item.Quantity;
        db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = finalStatus, Note = reason, ChangedAtUtc = now });

        Refund? refund = null;
        var paid = string.Equals(parent.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) || parent.PaymentStatus is "PartiallyRefunded";
        if (paid)
        {
            var payment = await db.Payments.Where(x => x.OrderId == parent.Id && x.Status == "Paid").OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (payment is not null && !await db.Refunds.AnyAsync(x => x.SellerOrderId == sellerOrder.Id && x.Status != "Failed", cancellationToken))
            {
                refund = new Refund { PaymentId = payment.Id, SellerOrderId = sellerOrder.Id, Amount = sellerOrder.Total, Reason = reason, Status = "Requested", CreatedAtUtc = now };
                db.Refunds.Add(refund);
            }
        }
        var siblings = await db.SellerOrders.Where(x => x.OrderId == parent.Id && x.Id != sellerOrder.Id).Select(x => x.Status).ToListAsync(cancellationToken);
        var allDead = siblings.All(s => s is "Rejected" or "Cancelled");
        parent.PaymentStatus = !paid ? (allDead ? "Cancelled" : parent.PaymentStatus) : allDead ? "Refunded" : "PartiallyRefunded";
        await NotifyBusinessesAsync([parent.RetailerBusinessId], title, message, $"/orders/{parent.Id}", now, cancellationToken);
        await NotifyBusinessesAsync([sellerOrder.ManufacturerBusinessId], title, $"Order {parent.OrderNumber} was {finalStatus.ToLowerInvariant()}.", "/seller/orders", now, cancellationToken);
        return refund;
    }

    private async Task ProcessRefundAsync(Refund refund, CancellationToken cancellationToken)
    {
        if (gateway is null || refund.Status != "Requested") return;
        var gatewayPaymentId = await db.Payments.Where(x => x.Id == refund.PaymentId).Select(x => x.GatewayPaymentId).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(gatewayPaymentId)) return;
        try
        {
            refund.GatewayRefundId = await gateway.RefundAsync(gatewayPaymentId, refund.Amount, $"refund-{refund.Id}", cancellationToken);
            refund.Status = "Processed";
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            // Stays "Requested"; the next maintenance run retries it.
        }
    }

    private async Task RetryRequestedRefundsAsync(CancellationToken cancellationToken)
    {
        if (gateway is null) return;
        foreach (var refund in await db.Refunds.Where(x => x.Status == "Requested").OrderBy(x => x.Id).Take(100).ToListAsync(cancellationToken))
            await ProcessRefundAsync(refund, cancellationToken);
    }

    private async Task NotifyBusinessesAsync(IEnumerable<long> businessIds, string title, string message, string link, DateTime now, CancellationToken cancellationToken)
    {
        var ids = businessIds.Distinct().ToArray();
        var users = await db.BusinessUsers.AsNoTracking().Where(x => ids.Contains(x.BusinessId)).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        db.Notifications.AddRange(users.Select(userId => new Notification { UserId = userId, Title = title, Message = message, Link = link, CreatedAtUtc = now }));
    }

    private async Task<int> UnpaidExpiryMinutesAsync(CancellationToken cancellationToken)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == "Orders.UnpaidExpiryMinutes").Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 5 and <= 10080 ? minutes : DefaultUnpaidExpiryMinutes;
    }
}
