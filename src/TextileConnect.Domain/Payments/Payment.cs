namespace TextileConnect.Domain.Payments;

public sealed class Payment { public long Id { get; set; } public long OrderId { get; set; } public string Gateway { get; set; } = "Razorpay"; public required string GatewayOrderId { get; set; } public string? GatewayPaymentId { get; set; } public decimal Amount { get; set; } public string Status { get; set; } = "Created"; public DateTime? PaidAt { get; set; } }
