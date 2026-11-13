using TextileConnect.Application.Orders;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;

namespace TextileConnect.Api;

public sealed record ProductResponse(long Id, long SellerBusinessId, long CategoryId, string Title, string Slug, string? Description, decimal GstRate, int Moq, ProductUnit Unit, ProductStatus Status, bool IsDeleted)
{
    public static ProductResponse From(Product value) => new(value.Id, value.SellerBusinessId, value.CategoryId, value.Title, value.Slug, value.Description, value.GstRate, value.Moq, value.Unit, value.Status, value.IsDeleted);
}

public sealed record ProductVariantResponse(long Id, long ProductId, string Sku, string? Size, string? Colour, int StockQty, bool IsActive)
{
    public static ProductVariantResponse From(ProductVariant value) => new(value.Id, value.ProductId, value.Sku, value.Size, value.Colour, value.StockQty, value.IsActive);
}

public sealed record PriceSlabResponse(long Id, long ProductId, int MinQty, int? MaxQty, decimal PricePerUnit)
{
    public static PriceSlabResponse From(PriceSlab value) => new(value.Id, value.ProductId, value.MinQty, value.MaxQty, value.PricePerUnit);
}

public sealed record ProductMediaResponse(long Id, long ProductId, string FileKey, string MediaType, int SortOrder)
{
    public static ProductMediaResponse From(ProductMedia value) => new(value.Id, value.ProductId, value.FileKey, value.MediaType, value.SortOrder);
}

public sealed record ProductDetailResponse(ProductResponse Product, IReadOnlyList<ProductVariantResponse> Variants, IReadOnlyList<PriceSlabResponse> Slabs, IReadOnlyList<ProductMediaResponse> Media)
{
    public static ProductDetailResponse From(ProductDetail value) => new(ProductResponse.From(value.Product), value.Variants.Select(ProductVariantResponse.From).ToList(), value.Slabs.Select(PriceSlabResponse.From).ToList(), value.Media.Select(ProductMediaResponse.From).ToList());
}

public sealed record CartLineResponse(long ItemId, long VariantId, int Quantity, decimal SlabPrice, decimal GstPerUnit, decimal? ShippingPerUnit, decimal Total)
{
    public static CartLineResponse From(CartLine value) => new(value.ItemId, value.VariantId, value.Quantity, value.SlabPrice, value.GstPerUnit, value.ShippingPerUnit, value.Total);
}

public sealed record CartSellerGroupResponse(long SellerBusinessId, IReadOnlyList<CartLineResponse> Items, decimal SubTotal, decimal GstTotal, decimal? ShippingTotal, decimal Total)
{
    public static CartSellerGroupResponse From(CartSellerGroup value) => new(value.SellerBusinessId, value.Items.Select(CartLineResponse.From).ToList(), value.SubTotal, value.GstTotal, value.ShippingTotal, value.Total);
}

public sealed record CartResponse(IReadOnlyList<CartSellerGroupResponse> Sellers)
{
    public static CartResponse From(CartView value) => new(value.Sellers.Select(CartSellerGroupResponse.From).ToList());
}

public sealed record OrderItemResponse(long Id, long SellerOrderId, long ProductId, int Quantity, decimal UnitPrice, decimal LineTotal)
{
    public static OrderItemResponse From(OrderItem value) => new(value.Id, value.SellerOrderId, value.ProductId, value.Quantity, value.UnitPrice, value.LineTotal);
}

public sealed record ShipmentResponse(long Id, long SellerOrderId, long? LogisticsBusinessId, string? CarrierName, string? LrNumber, string Status, DateTime? DispatchedAt, DateTime? DeliveredAt, string? ProofOfDeliveryKey)
{
    public static ShipmentResponse From(Shipment value) => new(value.Id, value.SellerOrderId, value.LogisticsBusinessId, value.CarrierName, value.LrNumber, value.Status, value.DispatchedAt, value.DeliveredAt, value.ProofOfDeliveryKey);
}

public sealed record SellerOrderResponse(long Id, long OrderId, long ManufacturerBusinessId, decimal SubTotal, decimal CommissionRate, string Status, string? RejectReason, DateTime? AcceptedAt, DateTime? DispatchedAt, DateTime? DeliveredAt, IReadOnlyList<OrderItemResponse> Items, ShipmentResponse? Shipment, OrderResponse? Order)
{
    public static SellerOrderResponse From(SellerOrder value, bool includeOrder = false) => new(value.Id, value.OrderId, value.ManufacturerBusinessId, value.SubTotal, value.CommissionRate, value.Status, value.RejectReason, value.AcceptedAt, value.DispatchedAt, value.DeliveredAt, value.Items.Select(OrderItemResponse.From).ToList(), null, includeOrder && value.Order is not null ? OrderResponse.From(value.Order, false) : null);
}

public sealed record OrderResponse(long Id, string OrderNumber, long RetailerBusinessId, string PlacedByUserId, decimal GrandTotal, string PaymentStatus, IReadOnlyList<SellerOrderResponse> SellerOrders)
{
    public static OrderResponse From(Order value, bool includeSellerOrders = true) => new(value.Id, value.OrderNumber, value.RetailerBusinessId, value.PlacedByUserId, value.GrandTotal, value.PaymentStatus, includeSellerOrders ? value.SellerOrders.Select(x => SellerOrderResponse.From(x)).ToList() : Array.Empty<SellerOrderResponse>());
}

public static class ApiResponseMappings
{
    public static object Product(Product value) => ProductResponse.From(value);
    public static object ProductDetail(ProductDetail value) => ProductDetailResponse.From(value);
    public static object Cart(CartView value) => CartResponse.From(value);
    public static object Order(Order value) => OrderResponse.From(value);
    public static object SellerOrder(SellerOrder value) => SellerOrderResponse.From(value);
    public static object Shipment(Shipment value) => ShipmentResponse.From(value);
}
