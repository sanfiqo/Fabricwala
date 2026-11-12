namespace TextileConnect.Domain.Catalogue;

public sealed class PriceSlab
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public int MinQty { get; set; }
    public int? MaxQty { get; set; }
    public decimal PricePerUnit { get; set; }
}
