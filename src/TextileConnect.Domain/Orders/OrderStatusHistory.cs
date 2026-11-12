namespace TextileConnect.Domain.Orders;

public sealed class OrderStatusHistory { public long Id { get; set; } public long SellerOrderId { get; set; } public required string Status { get; set; } public string? Note { get; set; } public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; }
