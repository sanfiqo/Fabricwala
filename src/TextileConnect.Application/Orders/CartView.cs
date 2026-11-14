namespace TextileConnect.Application.Orders;

using TextileConnect.Domain.Catalogue;

public sealed record CartLine(long ItemId, long VariantId, int Quantity, decimal SlabPrice, decimal GstPerUnit, decimal? ShippingPerUnit, decimal Total)
{
    public long ProductId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Size { get; init; }
    public string? Colour { get; init; }
    public string? ImageUrl { get; init; }
    public int Moq { get; init; }
    public ProductUnit Unit { get; init; }
    /// <summary>Total pieces of this product in the cart across sizes/colours; the price tier uses this.</summary>
    /// <summary>GST percentage (5 means 5%).</summary>
    public decimal GstRate { get; init; }
    public int ProductQuantity { get; init; }
    public decimal LineSubTotal { get; init; }
    public decimal LineGst { get; init; }
    public int AvailableStock { get; init; }
    public CartNextTier? NextTier { get; init; }
    /// <summary>Stable codes: unavailable, below_moq, insufficient_stock.</summary>
    public IReadOnlyList<string> Issues { get; init; } = [];
}
public sealed record CartSellerGroup(long SellerBusinessId, IReadOnlyList<CartLine> Items, decimal SubTotal, decimal GstTotal, decimal? ShippingTotal, decimal Total)
{
    public string ManufacturerName { get; init; } = string.Empty;
}
public sealed record CartView(IReadOnlyList<CartSellerGroup> Sellers)
{
    public bool CanCheckout => Sellers.Count > 0 && Sellers.All(x => x.Items.All(i => i.Issues.Count == 0));
}
