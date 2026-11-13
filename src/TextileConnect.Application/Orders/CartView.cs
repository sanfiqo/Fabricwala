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
}
public sealed record CartSellerGroup(long SellerBusinessId, IReadOnlyList<CartLine> Items, decimal SubTotal, decimal GstTotal, decimal? ShippingTotal, decimal Total)
{
    public string ManufacturerName { get; init; } = string.Empty;
}
public sealed record CartView(IReadOnlyList<CartSellerGroup> Sellers);
