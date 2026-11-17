using TextileConnect.Domain;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Payments;
using TextileConnect.Domain.Notifications;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

public sealed partial class EfPaymentService(MarketplaceDbContext db, IPaymentGateway gateway, TimeProvider? clock = null, IAuditTrail? audit = null) : IPaymentService
{
    private const string Scope = "payment";
    private const int DefaultAcceptWindowHours = 24;

    [GeneratedRegex("^[A-Za-z0-9._:-]{8,100}$")]
    private static partial Regex KeyPattern();

    public async Task<PaymentSession> StartAsync(long retailerBusinessId, long orderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!KeyPattern().IsMatch(idempotencyKey ?? string.Empty)) throw new ArgumentException("Idempotency-Key must be 8 to 100 letters, digits or . _ : -.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Scope}:{orderId}"))).ToLowerInvariant();
        var previous = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.BusinessId == retailerBusinessId && x.Scope == Scope && x.Key == idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (previous.RequestHash != hash) throw new RuleViolationException("idempotency_key_reused", "This Idempotency-Key was already used for a different request.");
            return await GetAsync(retailerBusinessId, previous.ResourceKey, cancellationToken) ?? throw new KeyNotFoundException("Payment not found.");
        }

        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId && x.RetailerBusinessId == retailerBusinessId, cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");
        if (!string.Equals(order.PaymentStatus, PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase))
            throw new RuleViolationException("order_not_payable", "This order is not waiting for payment.");
        var payment = await db.Payments.Where(x => x.OrderId == order.Id && x.Status == PaymentStatus.Pending).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            var created = await gateway.CreateOrderAsync(order.GrandTotal, order.OrderNumber, cancellationToken);
            payment = new Payment { OrderId = order.Id, GatewayOrderId = created.GatewayOrderId, Amount = created.Amount, Status = PaymentStatus.Pending };
            db.Payments.Add(payment);
        }
        db.IdempotencyRecords.Add(new IdempotencyRecord { BusinessId = retailerBusinessId, Scope = Scope, Key = idempotencyKey, RequestHash = hash, ResourceKey = payment.GatewayOrderId });
        await db.SaveChangesAsync(cancellationToken);
        return new PaymentSession(order.Id, order.OrderNumber, payment.GatewayOrderId, payment.Amount, payment.Status);
    }

    public async Task<PaymentSession?> GetAsync(long retailerBusinessId, string gatewayOrderId, CancellationToken cancellationToken = default)
        => await (from payment in db.Payments.AsNoTracking()
                  join order in db.Orders.AsNoTracking() on payment.OrderId equals order.Id
                  where payment.GatewayOrderId == gatewayOrderId && order.RetailerBusinessId == retailerBusinessId
                  select new PaymentSession(order.Id, order.OrderNumber, payment.GatewayOrderId, payment.Amount, payment.Status)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> BeginWebhookEventAsync(string gateway, string eventId, string eventType, CancellationToken cancellationToken = default)
    {
        var existing = await db.PaymentWebhookEvents.SingleOrDefaultAsync(x => x.Gateway == gateway && x.EventId == eventId, cancellationToken);
        if (existing is not null) return existing.ProcessedAtUtc is null;
        var row = new PaymentWebhookEvent { Gateway = gateway, EventId = eventId, EventType = eventType, ReceivedAtUtc = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime };
        db.PaymentWebhookEvents.Add(row);
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException)
        {
            // The same event arrived twice at the same moment and the other delivery stored it first: that one handles it.
            db.Entry(row).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteWebhookEventAsync(string gateway, string eventId, CancellationToken cancellationToken = default)
    {
        var row = await db.PaymentWebhookEvents.SingleOrDefaultAsync(x => x.Gateway == gateway && x.EventId == eventId, cancellationToken);
        if (row is null || row.ProcessedAtUtc is not null) return;
        row.ProcessedAtUtc = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ConfirmAsync(string gatewayOrderId, string gatewayPaymentId, decimal? amountPaid, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.GatewayOrderId == gatewayOrderId, cancellationToken);
        if (payment is null) return false;
        if (string.Equals(payment.Status, PaymentStatus.Paid, StringComparison.OrdinalIgnoreCase)) return true;
        if (amountPaid.HasValue && amountPaid.Value != payment.Amount)
            throw new RuleViolationException("payment_amount_mismatch", "The paid amount does not match the order.", 400);

        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        payment.Status = PaymentStatus.Paid;
        payment.GatewayPaymentId = gatewayPaymentId;
        payment.PaidAt = now;
        audit?.Record("payment.paid", nameof(Payment), payment.Id, $"order {payment.OrderId}, amount {AuditText.Money(payment.Amount)}");
        var order = await db.Orders.Include(x => x.SellerOrders).SingleAsync(x => x.Id == payment.OrderId, cancellationToken);
        if (order.SellerOrders.Count > 0 && order.SellerOrders.All(x => SellerOrderStatus.IsDead(x.Status)))
        {
            // Money arrived after the order was cancelled (payment window expired): queue a full refund, never keep it.
            order.PaymentStatus = PaymentStatus.Refunded;
            audit?.Record("payment.late_refund", nameof(Payment), payment.Id, $"order {payment.OrderId}, paid after cancellation");
            foreach (var cancelled in order.SellerOrders)
                if (cancelled.Total > 0) db.Refunds.Add(new Refund { PaymentId = payment.Id, SellerOrderId = cancelled.Id, Amount = cancelled.Total, Reason = "Payment arrived after the order was cancelled.", CreatedAtUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        order.PaymentStatus = PaymentStatus.Paid;
        var window = await AcceptWindowHoursAsync(cancellationToken);
        foreach (var sellerOrder in order.SellerOrders)
        {
            sellerOrder.AcceptDeadlineUtc = now.AddHours(window);
            // Seller's share is held (Pending, no release date) until the order is shipped or delivered.
            db.Settlements.Add(new Settlement { PaymentId = payment.Id, SellerOrderId = sellerOrder.Id, ManufacturerBusinessId = sellerOrder.ManufacturerBusinessId,
                Amount = sellerOrder.Total - sellerOrder.CommissionAmount, CommissionAmount = sellerOrder.CommissionAmount, Status = SettlementStatus.Pending, ReleaseAfter = Settlements.NoReleaseDate });
            db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = SellerOrderStatus.Pending, Note = "Payment received; waiting for the seller to accept.", ChangedAtUtc = now });
        }
        await NotifyAsync(order.SellerOrders.Select(x => x.ManufacturerBusinessId), "New order", $"Order {order.OrderNumber} is paid and waiting for you to accept.", "/seller/orders", now, cancellationToken);
        await NotifyAsync([order.RetailerBusinessId], "Payment received", $"Payment for order {order.OrderNumber} was received.", $"/orders/{order.Id}", now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> FailAsync(string gatewayOrderId, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.GatewayOrderId == gatewayOrderId, cancellationToken);
        if (payment is null) return false;
        if (string.Equals(payment.Status, PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = PaymentStatus.Failed;
            audit?.Record("payment.failed", nameof(Payment), payment.Id, $"order {payment.OrderId}");
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    private async Task NotifyAsync(IEnumerable<long> businessIds, string title, string message, string link, DateTime now, CancellationToken cancellationToken)
    {
        var ids = businessIds.Distinct().ToArray();
        var users = await db.BusinessUsers.AsNoTracking().Where(x => ids.Contains(x.BusinessId)).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        db.Notifications.AddRange(users.Select(userId => new Notification { UserId = userId, Title = title, Message = message, Link = link, CreatedAtUtc = now }));
    }

    private async Task<int> AcceptWindowHoursAsync(CancellationToken cancellationToken)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == "Seller.AcceptWindowHours").Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) && hours is >= 1 and <= 720 ? hours : DefaultAcceptWindowHours;
    }
}
