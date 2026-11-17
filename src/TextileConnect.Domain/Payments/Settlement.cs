namespace TextileConnect.Domain.Payments;

public sealed class Settlement : ITimestamped { public long Id { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow; public long PaymentId { get; set; } public long SellerOrderId { get; set; } public long ManufacturerBusinessId { get; set; } public decimal Amount { get; set; } public decimal CommissionAmount { get; set; } public string Status { get; set; } = SettlementStatus.Pending; public DateTime ReleaseAfter { get; set; } public DateTime? SettledAt { get; set; } }
