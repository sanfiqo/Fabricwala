namespace TextileConnect.Domain.Catalogue;

public sealed class ProductVariant
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public required string Sku { get; set; }
    public string? Size { get; set; }
    public string? Colour { get; set; }
    public int StockQty { get; set; }
    public bool IsActive { get; set; } = true;
}
