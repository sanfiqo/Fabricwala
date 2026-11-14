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

public sealed partial class EfPaymentService(MarketplaceDbContext db, IPaymentGateway gateway) : IPaymentService
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
        if (!string.Equals(order.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new RuleViolationException("order_not_payable", "This order is not waiting for payment.");
        var payment = await db.Payments.Where(x => x.OrderId == order.Id && x.Status == "Pending").OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            var created = await gateway.CreateOrderAsync(order.GrandTotal, order.OrderNumber, cancellationToken);
            payment = new Payment { OrderId = order.Id, GatewayOrderId = created.GatewayOrderId, Amount = created.Amount, Status = "Pending" };
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

    public async Task<bool> ConfirmAsync(string gatewayOrderId, string gatewayPaymentId, decimal? amountPaid, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.GatewayOrderId == gatewayOrderId, cancellationToken);
        if (payment is null) return false;
        if (string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase)) return true;
        if (amountPaid.HasValue && amountPaid.Value != payment.Amount)
            throw new RuleViolationException("payment_amount_mismatch", "The paid amount does not match the order.", 400);

        var now = DateTime.UtcNow;
        payment.Status = "Paid";
        payment.GatewayPaymentId = gatewayPaymentId;
        payment.PaidAt = now;
        var order = await db.Orders.Include(x => x.SellerOrders).SingleAsync(x => x.Id == payment.OrderId, cancellationToken);
        order.PaymentStatus = "Paid";
        var window = await AcceptWindowHoursAsync(cancellationToken);
        foreach (var sellerOrder in order.SellerOrders)
        {
            sellerOrder.AcceptDeadlineUtc = now.AddHours(window);
            db.OrderStatusHistories.Add(new OrderStatusHistory { SellerOrderId = sellerOrder.Id, Status = "Pending", Note = "Payment received; waiting for the seller to accept.", ChangedAtUtc = now });
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
        if (string.Equals(payment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = "Failed";
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
