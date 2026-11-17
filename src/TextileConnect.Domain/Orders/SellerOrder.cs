namespace TextileConnect.Domain.Orders;

public sealed class SellerOrder
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ManufacturerBusinessId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TaxAmount { get; set; }
    /// <summary>Platform fee: SubTotal x CommissionRate, charged to the seller.</summary>
    public decimal CommissionAmount { get; set; }
    /// <summary>SubTotal + ShippingAmount + TaxAmount (the part of the retailer's payment for this seller).</summary>
    public decimal Total { get; set; }
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptDeadlineUtc { get; set; }
    public string Status { get; set; } = SellerOrderStatus.Pending;
    public string? RejectReason { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public Order? Order { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> History { get; set; } = new List<OrderStatusHistory>();
}
