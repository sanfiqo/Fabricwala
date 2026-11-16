namespace TextileConnect.Application.Payments;

public sealed record PaymentOrder(string GatewayOrderId, decimal Amount);

public interface IPaymentGateway
{
    Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default);
    bool VerifyWebhookSignature(string payload, string signature);

    /// <summary>Refunds part or all of a captured payment and returns the gateway refund id. Throws when the gateway refuses.</summary>
    Task<string> RefundAsync(string gatewayPaymentId, decimal amount, string receipt, CancellationToken cancellationToken = default);
}

/// <summary>Fake gateway for Development and tests: nothing leaves the machine and no money moves.</summary>
public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentOrder($"dev_{Guid.NewGuid():N}", amount));
    public bool VerifyWebhookSignature(string payload, string signature) => RazorpaySignatureVerifier.Verify(payload, signature, "development-test-secret");
    public Task<string> RefundAsync(string gatewayPaymentId, decimal amount, string receipt, CancellationToken cancellationToken = default)
        => Task.FromResult($"dev_refund_{Guid.NewGuid():N}");
}
