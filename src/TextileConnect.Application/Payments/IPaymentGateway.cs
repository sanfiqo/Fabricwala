namespace TextileConnect.Application.Payments;

public sealed record PaymentOrder(string GatewayOrderId, decimal Amount);

public interface IPaymentGateway
{
    Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default);
    bool VerifyWebhookSignature(string payload, string signature);
}

public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentOrder($"dev_{Guid.NewGuid():N}", amount));
    public bool VerifyWebhookSignature(string payload, string signature) => RazorpaySignatureVerifier.Verify(payload, signature, "development-test-secret");
}
