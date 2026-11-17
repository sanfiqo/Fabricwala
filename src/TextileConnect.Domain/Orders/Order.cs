namespace TextileConnect.Domain.Orders;

public sealed class Order
{
    public long Id { get; set; }
    public required string OrderNumber { get; set; }
    public long RetailerBusinessId { get; set; }
    public string PlacedByUserId { get; set; } = string.Empty;
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal SubTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }
    /// <summary>What the retailer pays: goods + GST + delivery. The platform fee is charged to the seller.</summary>
    public decimal GrandTotal { get; set; }
    public string DeliveryLine1 { get; set; } = string.Empty;
    public string? DeliveryLine2 { get; set; }
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryPincode { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = TextileConnect.Domain.PaymentStatus.Pending;
    public ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();
}
