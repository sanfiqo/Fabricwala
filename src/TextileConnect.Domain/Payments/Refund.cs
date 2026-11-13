namespace TextileConnect.Domain.Payments;

public sealed class Refund { public long Id { get; set; } public long PaymentId { get; set; } public long SellerOrderId { get; set; } public decimal Amount { get; set; } public required string Reason { get; set; } public string Status { get; set; } = "Requested"; }
