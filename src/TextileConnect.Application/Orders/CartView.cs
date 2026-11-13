namespace TextileConnect.Application.Orders;

public sealed record CartLine(long ItemId, long VariantId, int Quantity, decimal SlabPrice, decimal GstPerUnit, decimal? ShippingPerUnit, decimal Total);
public sealed record CartSellerGroup(long SellerBusinessId, IReadOnlyList<CartLine> Items, decimal SubTotal, decimal GstTotal, decimal? ShippingTotal, decimal Total);
public sealed record CartView(IReadOnlyList<CartSellerGroup> Sellers);
