namespace TextileConnect.Application.Payments;

/// <summary>Payment state shown to the retailer. Status: Pending, Paid or Failed.</summary>
public sealed record PaymentSession(long OrderId, string OrderNumber, string GatewayOrderId, decimal Amount, string Status);

public interface IPaymentService
{
    /// <summary>Starts (or returns the existing pending) gateway payment for the retailer's own unpaid order. Idempotent per key.</summary>
    Task<PaymentSession> StartAsync(long retailerBusinessId, long orderId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<PaymentSession?> GetAsync(long retailerBusinessId, string gatewayOrderId, CancellationToken cancellationToken = default);

    /// <summary>Marks the payment paid once. Repeated calls change nothing. Returns false when the gateway order is unknown.</summary>
    Task<bool> ConfirmAsync(string gatewayOrderId, string gatewayPaymentId, decimal? amountPaid, CancellationToken cancellationToken = default);

    /// <summary>Marks a pending payment failed; a paid payment is never downgraded. Returns false when unknown.</summary>
    Task<bool> FailAsync(string gatewayOrderId, CancellationToken cancellationToken = default);
}
