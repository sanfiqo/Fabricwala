namespace TextileConnect.Domain.Payments;

public sealed class Invoice { public long Id { get; set; } public long SellerOrderId { get; set; } public required string InvoiceNumber { get; set; } public string InvoiceType { get; set; } = "SellerToRetailer"; public string? FileKey { get; set; } public DateTime IssuedAt { get; set; } = DateTime.UtcNow; }
