using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;

namespace TextileConnect.Api;

public sealed record ProductSummaryResponse(long Id, string Slug, string Title, long CategoryId, long SellerBusinessId, string SellerName, string SellerCity, string? ImageUrl, int Moq, ProductUnit Unit, decimal PriceFrom, decimal PriceTo, string? Material, ProductStatus? Status)
{
    public static ProductSummaryResponse From(Product value) => new(value.Id, value.Slug, value.Title, value.CategoryId, value.SellerBusinessId, string.Empty, string.Empty, null, value.Moq, value.Unit, 0m, 0m, null, value.Status);
}

public sealed record ProductVariantResponse(long Id, string Sku, string? Size, string? Colour, int StockQty, bool IsActive)
{
    public static ProductVariantResponse From(ProductVariant value) => new(value.Id, value.Sku, value.Size, value.Colour, value.StockQty, value.IsActive);
}

public sealed record PriceSlabResponse(long? Id, int MinQty, int? MaxQty, decimal PricePerUnit)
{
    public static PriceSlabResponse From(PriceSlab value) => new(value.Id, value.MinQty, value.MaxQty, value.PricePerUnit);
}

public sealed record ProductMediaResponse(long Id, string? Url, string MediaType, int SortOrder)
{
    public static ProductMediaResponse From(ProductMedia value) => new(value.Id, value.FileKey, value.MediaType, value.SortOrder);
}

public sealed record ProductDetailResponse(long Id, string Slug, string Title, long CategoryId, long SellerBusinessId, string SellerName, string SellerCity, string? ImageUrl, int Moq, ProductUnit Unit, decimal PriceFrom, decimal PriceTo, string? Material, string? Description, string? FabricSpec, string? Season, string? HsnCode, decimal GstRate, decimal WeightPerUnitGrams, int DispatchDays, string? ReturnPolicy, ProductStatus Status, IReadOnlyList<ProductMediaResponse> Media, IReadOnlyList<ProductVariantResponse> Variants, IReadOnlyList<PriceSlabResponse> Slabs)
{
    public static ProductDetailResponse From(ProductDetail value)
    {
        var prices = value.Slabs.Select(x => x.PricePerUnit).ToArray();
        return new(value.Product.Id, value.Product.Slug, value.Product.Title, value.Product.CategoryId, value.Product.SellerBusinessId, string.Empty, string.Empty, null, value.Product.Moq, value.Product.Unit, prices.DefaultIfEmpty().Min(), prices.DefaultIfEmpty().Max(), null, value.Product.Description, null, null, null, value.Product.GstRate, 0m, 0, null, value.Product.Status, value.Media.Select(ProductMediaResponse.From).ToList(), value.Variants.Select(ProductVariantResponse.From).ToList(), value.Slabs.Select(PriceSlabResponse.From).ToList());
    }
}

public sealed record CartItemResponse(long Id, long VariantId, long ProductId, string Slug, string Title, string? Size, string? Colour, string? ImageUrl, int Quantity, int Moq, ProductUnit Unit, decimal UnitPrice, decimal LineTotal);
public sealed record CartGroupResponse(long ManufacturerId, string ManufacturerName, IReadOnlyList<CartItemResponse> Items, decimal SubTotal, decimal? ShippingAmount, decimal TaxAmount, decimal Total);
public sealed record CartResponse(IReadOnlyList<CartGroupResponse> Groups, decimal SubTotal, decimal? ShippingTotal, decimal TaxTotal, decimal GrandTotal, IReadOnlyList<object> Addresses)
{
    public static CartResponse From(CartView value) => new(value.Sellers.Select(group => new CartGroupResponse(group.SellerBusinessId, group.ManufacturerName, group.Items.Select(line => new CartItemResponse(line.ItemId, line.VariantId, line.ProductId, line.Slug, line.Title, line.Size, line.Colour, line.ImageUrl, line.Quantity, line.Moq, line.Unit, line.SlabPrice, line.Total)).ToList(), group.SubTotal, group.ShippingTotal, group.GstTotal, group.Total)).ToList(), value.Sellers.Sum(x => x.SubTotal), value.Sellers.Any(x => x.ShippingTotal is null) ? null : value.Sellers.Sum(x => x.ShippingTotal ?? 0), value.Sellers.Sum(x => x.GstTotal), value.Sellers.Sum(x => x.Total), []);
}

public sealed record OrderItemResponse(long Id, long ProductId, string Title, string? Size, string? Colour, int Quantity, decimal UnitPrice, decimal GstRate, decimal LineTotal);
public sealed record StatusHistoryResponse(string Status, string? Note, DateTime ChangedAt);
public sealed record ShipmentResponse(string? CarrierName, string? LrNumber, DateTime? DispatchedAt);
public sealed record SellerOrderResponse(long Id, long OrderId, string OrderNumber, string SubOrderNumber, string ManufacturerName, string RetailerName, string DeliveryCity, string Status, string PaymentStatus, DateTime? PlacedAt, IReadOnlyList<OrderItemResponse> Items, decimal SubTotal, decimal ShippingAmount, decimal TaxAmount, decimal Total, decimal CommissionRate, decimal CommissionAmount, string? RejectReason, IReadOnlyList<StatusHistoryResponse> History, ShipmentResponse? Shipment)
{
    public static SellerOrderResponse From(SellerOrder value) => new(value.Id, value.OrderId, value.Order?.OrderNumber ?? string.Empty, $"{value.Order?.OrderNumber ?? ""}-{value.Id}", string.Empty, string.Empty, string.Empty, value.Status == "Pending" ? "PendingAcceptance" : value.Status, value.Order?.PaymentStatus ?? "Pending", null, value.Items.Select(x => new OrderItemResponse(x.Id, x.ProductId, string.Empty, null, null, x.Quantity, x.UnitPrice, 0m, x.LineTotal)).ToList(), value.SubTotal, 0m, 0m, value.SubTotal, value.CommissionRate, value.SubTotal * value.CommissionRate, value.RejectReason, [], null);
}

public sealed record OrderResponse(long Id, string OrderNumber, DateTime? PlacedAt, decimal SubTotal, decimal ShippingTotal, decimal TaxTotal, decimal GrandTotal, string PaymentStatus, object? DeliveryAddress, IReadOnlyList<SellerOrderResponse> SellerOrders)
{
    public static OrderResponse From(Order value) => new(value.Id, value.OrderNumber, null, value.SellerOrders.Sum(x => x.SubTotal), 0m, 0m, value.GrandTotal, value.PaymentStatus, null, value.SellerOrders.Select(SellerOrderResponse.From).ToList());
}

public static class ApiResponseMappings
{
    public static ProductSummaryResponse Product(Product value) => ProductSummaryResponse.From(value);
    public static ProductDetailResponse ProductDetail(ProductDetail value) => ProductDetailResponse.From(value);
    public static CartResponse Cart(CartView value) => CartResponse.From(value);
    public static OrderResponse Order(Order value) => OrderResponse.From(value);
    public static SellerOrderResponse SellerOrder(SellerOrder value) => SellerOrderResponse.From(value);
    public static ShipmentResponse Shipment(Shipment value) => new(value.CarrierName, value.LrNumber, value.DispatchedAt);
}
