namespace TextileConnect.Domain.Orders;

public sealed class SellerOrder
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ManufacturerBusinessId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal CommissionRate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RejectReason { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public Order? Order { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
