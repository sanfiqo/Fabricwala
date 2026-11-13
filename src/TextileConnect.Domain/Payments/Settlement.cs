namespace TextileConnect.Domain.Payments;

public sealed class Settlement { public long Id { get; set; } public long PaymentId { get; set; } public long SellerOrderId { get; set; } public long ManufacturerBusinessId { get; set; } public decimal Amount { get; set; } public decimal CommissionAmount { get; set; } public string Status { get; set; } = "Pending"; public DateTime ReleaseAfter { get; set; } public DateTime? SettledAt { get; set; } }
