namespace TextileConnect.Domain.Payments;

public sealed class Refund
{
    public long Id { get; set; }
    public long PaymentId { get; set; }
    public long SellerOrderId { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    /// <summary>Requested (waiting for the gateway), Processed or Failed.</summary>
    public string Status { get; set; } = "Requested";
    public string? GatewayRefundId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
