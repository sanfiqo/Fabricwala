namespace TextileConnect.Domain.Orders;

public sealed class OrderItem { public long Id { get; set; } public long SellerOrderId { get; set; } public long ProductId { get; set; } public int Quantity { get; set; } public decimal UnitPrice { get; set; } public decimal LineTotal => Quantity * UnitPrice; public SellerOrder? SellerOrder { get; set; } }
