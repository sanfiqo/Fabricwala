using TextileConnect.Domain;
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

public sealed class EfOrderService(MarketplaceDbContext db, IPaymentGateway? gateway = null, TimeProvider? clock = null, IAuditTrail? audit = null) : IOrderService, IOrderMaintenance
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
        var sellerOrders = await db.SellerOrders.Include(x => x.Items).Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        var shipped = sellerOrders.Where(x => SellerOrderTransitions.CanMove(x.Status, SellerOrderStatus.Delivered)).ToList();
        if (shipped.Count == 0)
        {
            if (sellerOrders.Count > 0 && sellerOrders.All(x => x.Status == SellerOrderStatus.Delivered)) return order; // already confirmed: nothing changes
            throw new RuleViolationException("order_not_shipped", "This order has not been shipped yet.");
        }
        var now = Now;
        var shippedIds = shipped.Select(x => x.Id).ToArray();
        // Popularity counts pieces that really arrived. A concurrent delivery of the same product can lose one increment; for a sort order that is acceptable.
        var soldByProduct = shipped.SelectMany(x => x.Items).GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity));
        var soldProductIds = soldByProduct.Keys.ToArray();
        foreach (var product in await db.Products.Where(x => soldProductIds.Contains(x.Id)).ToListAsync(cancellationToken))
            product.UnitsSold += soldByProduct[product.Id];
        foreach (var sellerOrder in shipped)
        {
            sellerOrder.Status = SellerOrderStatus.Delivered;
            sellerOrder.DeliveredAt = now;
            audit?.Record("order.delivered", nameof(SellerOrder), sellerOrder.Id, $"order {order.OrderNumber}");
            db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = SellerOrderStatus.Delivered, Note = "Delivery confirmed by the retailer.", ChangedAtUtc = now });
        }
        foreach (var settlement in await db.Settlements.Where(x => shippedIds.Contains(x.SellerOrderId) && x.Status == SettlementStatus.Pending).ToListAsync(cancellationToken))
            settlement.ReleaseAfter = now;
        foreach (var shipment in await db.Shipments.Where(x => shippedIds.Contains(x.SellerOrderId)).ToListAsync(cancellationToken))
        { shipment.Status = ShipmentProgress.Delivered; shipment.DeliveredAt = now; }
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
            case SellerOrderStatus.Accepted:
                RequireTransition(order, parent, SellerOrderStatus.Accepted);
                if (order.AcceptDeadlineUtc is { } deadline && deadline <= now)
                    throw new RuleViolationException("accept_window_expired", "The time to accept this order has passed.");
                order.Status = SellerOrderStatus.Accepted;
                order.AcceptedAt = now;
                audit?.Record("order.accepted", nameof(SellerOrder), order.Id, $"order {parent.OrderNumber}");
                db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = order.Id, Status = SellerOrderStatus.Accepted, ChangedAtUtc = now });
                await NotifyBusinessesAsync([parent.RetailerBusinessId], "Order accepted", $"The seller accepted order {parent.OrderNumber}.", $"/orders/{parent.Id}", now, cancellationToken);
                break;
            case SellerOrderStatus.Rejected:
                RequireTransition(order, parent, SellerOrderStatus.Rejected);
                var rejection = reason?.Trim();
                if (string.IsNullOrEmpty(rejection) || rejection.Length > 500) throw new ArgumentException("A reason of up to 500 characters is required.");
                refund = await CancelAsync(order, parent, SellerOrderStatus.Rejected, rejection, "Order rejected", $"The seller could not accept order {parent.OrderNumber}. You will be refunded.", now, cancellationToken);
                break;
            case SellerOrderStatus.Dispatched:
                RequireTransition(order, parent, SellerOrderStatus.Dispatched);
                var lr = lrNumber?.Trim();
                var carrier = carrierName?.Trim();
                if (string.IsNullOrEmpty(lr) || lr.Length > 60 || carrier?.Length > 80) throw new ArgumentException("An LR/tracking number of up to 60 characters is required.");
                order.Status = SellerOrderStatus.Dispatched;
                order.DispatchedAt = now;
                audit?.Record("order.dispatched", nameof(SellerOrder), order.Id, $"order {parent.OrderNumber}");
                var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == order.Id, cancellationToken);
                if (shipment is null) db.Shipments.Add(new Shipment { SellerOrderId = order.Id, LrNumber = lr, CarrierName = carrier, DispatchedAt = now });
                else { shipment.LrNumber = lr; shipment.CarrierName = carrier; shipment.DispatchedAt = now; shipment.Status = ShipmentProgress.Dispatched; }
                db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = order.Id, Status = SellerOrderStatus.Dispatched, Note = $"LR {lr}", ChangedAtUtc = now });
                var autoRelease = now.AddDays(await AutoReleaseDaysAsync(cancellationToken));
                var held = await db.Settlements.SingleOrDefaultAsync(x => x.SellerOrderId == order.Id && x.Status == SettlementStatus.Pending, cancellationToken);
                if (held is not null && held.ReleaseAfter > autoRelease) held.ReleaseAfter = autoRelease;
                await NotifyBusinessesAsync([parent.RetailerBusinessId], "Order shipped", $"Order {parent.OrderNumber} was shipped. LR number {lr}.", $"/orders/{parent.Id}", now, cancellationToken);
                break;
            default:
                throw new ArgumentException("Unsupported status. Use Accepted, Rejected or Dispatched.");
        }
        await db.SaveChangesAsync(cancellationToken);
        if (refund is not null) await ProcessRefundAsync(refund, cancellationToken);
        return order;
    }

    /// <summary>The order must be paid and the move must be one that SellerOrderTransitions allows.</summary>
    private static void RequireTransition(SellerOrder order, Order parent, string to)
    {
        if (!string.Equals(parent.PaymentStatus, PaymentStatus.Paid, StringComparison.OrdinalIgnoreCase))
            throw new RuleViolationException("order_not_paid", "This order is not paid yet.");
        RequireMove(order, to);
    }

    private static void RequireMove(SellerOrder order, string to)
    {
        if (!SellerOrderTransitions.CanMove(order.Status, to))
            throw new RuleViolationException("invalid_order_transition", "This order cannot be changed in its current state.");
    }

    public async Task<Shipment?> UpdateShipmentAsync(long businessId, long sellerOrderId, string status, CancellationToken cancellationToken = default)
    {
        var sellerOrder = await db.SellerOrders.SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.ManufacturerBusinessId == businessId, cancellationToken);
        if (sellerOrder is null) return null;
        var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrderId, cancellationToken);
        if (shipment is null) return null;
        if (!ShipmentProgress.SellerCanSet.Contains(status))
            throw new RuleViolationException("invalid_shipment_status", "Use InTransit or ReachedCity. Delivery is confirmed by the retailer.", 400);
        if (sellerOrder.Status != SellerOrderStatus.Dispatched)
            throw new RuleViolationException("invalid_order_transition", "Only a shipped order can be updated.");
        shipment.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return shipment;
    }

    public async Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var now = Now;
        var cancelled = 0;
        var refunds = new List<Refund>();

        var late = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items)
            .Where(x => x.Status == SellerOrderStatus.Pending && x.Order!.PaymentStatus == PaymentStatus.Paid && x.AcceptDeadlineUtc != null && x.AcceptDeadlineUtc <= now)
            .OrderBy(x => x.Id).Take(200).ToListAsync(cancellationToken);
        foreach (var sellerOrder in late)
        {
            var refund = await CancelAsync(sellerOrder, sellerOrder.Order!, SellerOrderStatus.Cancelled, "The seller did not accept in time.", "Order cancelled",
                $"Order {sellerOrder.Order!.OrderNumber} was cancelled because it was not accepted in time. You will be refunded.", now, cancellationToken);
            if (refund is not null) refunds.Add(refund);
            cancelled++;
        }

        var cutoff = now.AddMinutes(-await UnpaidExpiryMinutesAsync(cancellationToken));
        var unpaid = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items)
            .Where(x => x.Status == SellerOrderStatus.Pending && x.Order!.PaymentStatus == PaymentStatus.Pending && x.Order.PlacedAtUtc <= cutoff)
            .OrderBy(x => x.Id).Take(200).ToListAsync(cancellationToken);
        foreach (var sellerOrder in unpaid)
        {
            await CancelAsync(sellerOrder, sellerOrder.Order!, SellerOrderStatus.Cancelled, "Payment was not completed in time.", "Order cancelled",
                $"Order {sellerOrder.Order!.OrderNumber} was cancelled because payment was not completed.", now, cancellationToken);
            cancelled++;
        }
        await ReleaseDueSettlementsAsync(now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var refund in refunds) await ProcessRefundAsync(refund, cancellationToken);
        await RetryRequestedRefundsAsync(cancellationToken);
        return cancelled;
    }

    /// <summary>Cancels a seller order: status, stock back on the shelf, full refund when paid, history and notifications. Caller saves.</summary>
    private async Task<Refund?> CancelAsync(SellerOrder sellerOrder, Order parent, string finalStatus, string reason, string title, string message, DateTime now, CancellationToken cancellationToken)
    {
        RequireMove(sellerOrder, finalStatus);
        sellerOrder.Status = finalStatus;
        sellerOrder.RejectReason = reason;
        audit?.Record("order." + finalStatus.ToLowerInvariant(), nameof(SellerOrder), sellerOrder.Id, $"order {parent.OrderNumber}: {reason}");
        var variantIds = sellerOrder.Items.Where(x => x.VariantId.HasValue).Select(x => x.VariantId!.Value).ToArray();
        var variants = await db.ProductVariants.Where(x => variantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in sellerOrder.Items.Where(x => x.VariantId.HasValue))
            if (variants.TryGetValue(item.VariantId!.Value, out var variant)) variant.StockQty += item.Quantity;
        db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = finalStatus, Note = reason, ChangedAtUtc = now });
        foreach (var settlement in await db.Settlements.Where(x => x.SellerOrderId == sellerOrder.Id && x.Status == SettlementStatus.Pending).ToListAsync(cancellationToken))
            settlement.Status = SettlementStatus.Cancelled;

        Refund? refund = null;
        var paid = string.Equals(parent.PaymentStatus, PaymentStatus.Paid, StringComparison.OrdinalIgnoreCase) || parent.PaymentStatus is PaymentStatus.PartiallyRefunded;
        if (paid)
        {
            var payment = await db.Payments.Where(x => x.OrderId == parent.Id && x.Status == PaymentStatus.Paid).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (payment is not null && !await db.Refunds.AnyAsync(x => x.SellerOrderId == sellerOrder.Id && x.Status != RefundStatus.Failed, cancellationToken))
            {
                refund = new Refund { PaymentId = payment.Id, SellerOrderId = sellerOrder.Id, Amount = sellerOrder.Total, Reason = reason, Status = RefundStatus.Requested, CreatedAtUtc = now };
                db.Refunds.Add(refund);
                audit?.Record("refund.requested", nameof(SellerOrder), sellerOrder.Id, $"amount {AuditText.Money(refund.Amount)}");
            }
        }
        var siblings = await db.SellerOrders.Where(x => x.OrderId == parent.Id && x.Id != sellerOrder.Id).Select(x => x.Status).ToListAsync(cancellationToken);
        var allDead = siblings.All(SellerOrderStatus.IsDead);
        parent.PaymentStatus = !paid ? (allDead ? PaymentStatus.Cancelled : parent.PaymentStatus) : allDead ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        await NotifyBusinessesAsync([parent.RetailerBusinessId], title, message, $"/orders/{parent.Id}", now, cancellationToken);
        await NotifyBusinessesAsync([sellerOrder.ManufacturerBusinessId], title, $"Order {parent.OrderNumber} was {finalStatus.ToLowerInvariant()}.", "/seller/orders", now, cancellationToken);
        return refund;
    }

    private async Task ProcessRefundAsync(Refund refund, CancellationToken cancellationToken)
    {
        if (gateway is null || refund.Status != RefundStatus.Requested) return;
        var gatewayPaymentId = await db.Payments.Where(x => x.Id == refund.PaymentId).Select(x => x.GatewayPaymentId).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(gatewayPaymentId)) return;
        try
        {
            refund.GatewayRefundId = await gateway.RefundAsync(gatewayPaymentId, refund.Amount, $"refund-{refund.Id}", cancellationToken);
            refund.Status = RefundStatus.Processed;
            audit?.Record("refund.processed", nameof(Refund), refund.Id, $"amount {AuditText.Money(refund.Amount)}");
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or Payments.PaymentGatewayException)
        {
            // Stays "Requested"; the next maintenance run retries it.
        }
    }

    private async Task RetryRequestedRefundsAsync(CancellationToken cancellationToken)
    {
        if (gateway is null) return;
        foreach (var refund in await db.Refunds.Where(x => x.Status == RefundStatus.Requested).OrderBy(x => x.Id).Take(100).ToListAsync(cancellationToken))
            await ProcessRefundAsync(refund, cancellationToken);
    }

    private async Task NotifyBusinessesAsync(IEnumerable<long> businessIds, string title, string message, string link, DateTime now, CancellationToken cancellationToken)
    {
        var ids = businessIds.Distinct().ToArray();
        var users = await db.BusinessUsers.AsNoTracking().Where(x => ids.Contains(x.BusinessId)).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        db.Notifications.AddRange(users.Select(userId => new Notification { UserId = userId, Title = title, Message = message, Link = link, CreatedAtUtc = now }));
    }

    /// <summary>Releases held payouts whose date has come, unless the retailer has an unresolved claim on that order.</summary>
    private async Task ReleaseDueSettlementsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var due = await db.Settlements.Where(x => x.Status == SettlementStatus.Pending && x.ReleaseAfter <= now
                && !db.ReturnClaims.Any(c => c.SellerOrderId == x.SellerOrderId && c.ResolvedAtUtc == null))
            .OrderBy(x => x.Id).Take(200).ToListAsync(cancellationToken);
        if (due.Count == 0) return;
        var orderNumbers = await db.SellerOrders.Where(x => due.Select(d => d.SellerOrderId).Contains(x.Id)).Select(x => new { x.Id, x.Order!.OrderNumber }).ToDictionaryAsync(x => x.Id, x => x.OrderNumber, cancellationToken);
        foreach (var settlement in due)
        {
            settlement.Status = SettlementStatus.Released;
            settlement.SettledAt = now;
            audit?.Record("settlement.released", nameof(Settlement), settlement.Id, $"amount {AuditText.Money(settlement.Amount)}");
            await NotifyBusinessesAsync([settlement.ManufacturerBusinessId], "Payout released", $"Your payout for order {orderNumbers.GetValueOrDefault(settlement.SellerOrderId)} is released.", "/seller/payouts", now, cancellationToken);
        }
    }

    private async Task<int> AutoReleaseDaysAsync(CancellationToken cancellationToken)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == "Settlement.AutoReleaseDays").Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days is >= 1 and <= 90 ? days : 7;
    }

    private async Task<int> UnpaidExpiryMinutesAsync(CancellationToken cancellationToken)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == "Orders.UnpaidExpiryMinutes").Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 5 and <= 10080 ? minutes : DefaultUnpaidExpiryMinutes;
    }
}
