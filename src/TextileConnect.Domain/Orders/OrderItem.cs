namespace TextileConnect.Domain.Orders;

/// <summary>Order line with a snapshot of the product details and prices at checkout.</summary>
public sealed class OrderItem
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public long ProductId { get; set; }
    public long? VariantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Colour { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>GST percentage (5 means 5%).</summary>
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
    /// <summary>HSN code of the product at checkout, printed on the GST invoice.</summary>
    public string? HsnCode { get; set; }
    /// <summary>Address of the main product photo (small copy) at checkout, so an order list can show the picture. Empty when the product had no photo.</summary>
    public string? ImageUrl { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
    public SellerOrder? SellerOrder { get; set; }
}
