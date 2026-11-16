using TextileConnect.Application.Payments;

namespace TextileConnect.Api;

/// <summary>Payment details for the checkout screen and the success/failed screen. Status: Pending, Paid or Failed.</summary>
public sealed record PaymentSessionResponse(long OrderId, string OrderNumber, string GatewayOrderId, decimal Amount, string Currency, string KeyId, string Status)
{
    public static PaymentSessionResponse From(PaymentSession session, string keyId)
        => new(session.OrderId, session.OrderNumber, session.GatewayOrderId, session.Amount, "INR", keyId, session.Status);
}
