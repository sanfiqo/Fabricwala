namespace TextileConnect.Domain.Orders;

public sealed class Order
{
    public long Id { get; set; }
    public required string OrderNumber { get; set; }
    public long RetailerBusinessId { get; set; }
    public string PlacedByUserId { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();
}
