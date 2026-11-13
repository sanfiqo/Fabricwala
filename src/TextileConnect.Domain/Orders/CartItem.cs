namespace TextileConnect.Domain.Orders;

public sealed class CartItem
{
    public long Id { get; set; }
    public long RetailerBusinessId { get; set; }
    public string AddedByUserId { get; set; } = string.Empty;
    public long VariantId { get; set; }
    public int Quantity { get; set; }
}
