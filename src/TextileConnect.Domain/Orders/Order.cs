namespace TextileConnect.Domain.Orders;

public sealed class Order { public long Id { get; set; } public required string OrderNumber { get; set; } public long RetailerBusinessId { get; set; } public decimal GrandTotal { get; set; } public string PaymentStatus { get; set; } = "Pending"; }
