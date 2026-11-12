namespace TextileConnect.Domain.Orders;

public sealed class SellerOrder { public long Id { get; set; } public long OrderId { get; set; } public long ManufacturerBusinessId { get; set; } public decimal SubTotal { get; set; } public decimal CommissionRate { get; set; } public string Status { get; set; } = "Pending"; }
