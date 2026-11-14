using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;

namespace TextileConnect.Api;

public sealed record ProductSummaryResponse(long Id, string Slug, string Title, long CategoryId, long SellerBusinessId, string SellerName, string SellerCity, string? ImageUrl, int Moq, ProductUnit Unit, decimal PriceFrom, decimal PriceTo, string? Material, ProductStatus? Status)
{
    public int AvailableStock { get; init; }
    public int UnitsSold { get; init; }
    public LandedCostResponse? LandedCost { get; init; }
    public static ProductSummaryResponse From(Product value) => new(value.Id, value.Slug, value.Title, value.CategoryId, value.SellerBusinessId, string.Empty, string.Empty, null, value.Moq, value.Unit, 0m, 0m, value.Material, value.Status);
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
    public LandedCostResponse? LandedCost { get; init; }
    public static ProductDetailResponse From(ProductDetail value)
    {
        var prices = value.Slabs.Select(x => x.PricePerUnit).ToArray();
        return new(value.Product.Id, value.Product.Slug, value.Product.Title, value.Product.CategoryId, value.Product.SellerBusinessId, string.Empty, string.Empty, null, value.Product.Moq, value.Product.Unit, prices.DefaultIfEmpty().Min(), prices.DefaultIfEmpty().Max(), value.Product.Material, value.Product.Description, value.Product.FabricSpec, value.Product.Season, value.Product.HsnCode, value.Product.GstRate, value.Product.WeightPerUnitGrams, value.Product.DispatchDays, value.Product.ReturnPolicy, value.Product.Status, value.Media.Select(ProductMediaResponse.From).ToList(), value.Variants.Select(ProductVariantResponse.From).ToList(), value.Slabs.Select(PriceSlabResponse.From).ToList());
    }
}

public sealed record CartItemResponse(long Id, long VariantId, long ProductId, string Slug, string Title, string? Size, string? Colour, string? ImageUrl, int Quantity, int Moq, ProductUnit Unit, decimal UnitPrice, decimal LineTotal)
{
    public int ProductQuantity { get; init; }
    public decimal LineSubTotal { get; init; }
    public decimal LineTax { get; init; }
    public int AvailableStock { get; init; }
    public TextileConnect.Application.Orders.CartNextTier? NextTier { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
}
public sealed record CartGroupResponse(long ManufacturerId, string ManufacturerName, IReadOnlyList<CartItemResponse> Items, decimal SubTotal, decimal? ShippingAmount, decimal TaxAmount, decimal Total);
public sealed record CartResponse(IReadOnlyList<CartGroupResponse> Groups, decimal SubTotal, decimal? ShippingTotal, decimal TaxTotal, decimal GrandTotal, IReadOnlyList<object> Addresses)
{
    public bool CanCheckout { get; init; }
    public static CartResponse From(CartView value) => new(value.Sellers.Select(group => new CartGroupResponse(group.SellerBusinessId, group.ManufacturerName, group.Items.Select(line => new CartItemResponse(line.ItemId, line.VariantId, line.ProductId, line.Slug, line.Title, line.Size, line.Colour, line.ImageUrl, line.Quantity, line.Moq, line.Unit, line.SlabPrice, line.Total) { ProductQuantity = line.ProductQuantity, LineSubTotal = line.LineSubTotal, LineTax = line.LineGst, AvailableStock = line.AvailableStock, NextTier = line.NextTier, Issues = line.Issues }).ToList(), group.SubTotal, group.ShippingTotal, group.GstTotal, group.Total)).ToList(), value.Sellers.Sum(x => x.SubTotal), value.Sellers.Any(x => x.ShippingTotal is null) ? null : value.Sellers.Sum(x => x.ShippingTotal ?? 0), value.Sellers.Sum(x => x.GstTotal), value.Sellers.Sum(x => x.Total), []) { CanCheckout = value.CanCheckout };
}

public sealed record OrderItemResponse(long Id, long ProductId, string Title, string? Size, string? Colour, int Quantity, decimal UnitPrice, decimal GstRate, decimal LineTotal)
{
    public decimal GstAmount { get; init; }
}
public sealed record StatusHistoryResponse(string Status, string? Note, DateTime ChangedAt);
public sealed record ShipmentResponse(string? CarrierName, string? LrNumber, DateTime? DispatchedAt);
public sealed record SellerOrderResponse(long Id, long OrderId, string OrderNumber, string SubOrderNumber, string ManufacturerName, string RetailerName, string DeliveryCity, string Status, string PaymentStatus, DateTime? PlacedAt, IReadOnlyList<OrderItemResponse> Items, decimal SubTotal, decimal ShippingAmount, decimal TaxAmount, decimal Total, decimal CommissionRate, decimal CommissionAmount, string? RejectReason, IReadOnlyList<StatusHistoryResponse> History, ShipmentResponse? Shipment)
{
    public static SellerOrderResponse From(SellerOrder value) => new(value.Id, value.OrderId, value.Order?.OrderNumber ?? string.Empty, $"{value.Order?.OrderNumber ?? ""}-{value.Id}", string.Empty, string.Empty, value.Order?.DeliveryCity ?? string.Empty, value.Status == "Pending" ? "PendingAcceptance" : value.Status, value.Order?.PaymentStatus ?? "Pending", value.PlacedAtUtc, value.Items.Select(x => new OrderItemResponse(x.Id, x.ProductId, x.Title, x.Size, x.Colour, x.Quantity, x.UnitPrice, x.GstRate, x.LineTotal) { GstAmount = x.GstAmount }).ToList(), value.SubTotal, value.ShippingAmount, value.TaxAmount, value.Total, value.CommissionRate, value.CommissionAmount, value.RejectReason, value.History.OrderBy(x => x.ChangedAtUtc).ThenBy(x => x.Id).Select(x => new StatusHistoryResponse(x.Status, x.Note, x.ChangedAtUtc)).ToList(), null);
}

public sealed record OrderResponse(long Id, string OrderNumber, DateTime? PlacedAt, decimal SubTotal, decimal ShippingTotal, decimal TaxTotal, decimal GrandTotal, string PaymentStatus, object? DeliveryAddress, IReadOnlyList<SellerOrderResponse> SellerOrders)
{
    public static OrderResponse From(Order value) => new(value.Id, value.OrderNumber, value.PlacedAtUtc, value.SubTotal, value.ShippingTotal, value.TaxTotal, value.GrandTotal, value.PaymentStatus, new { line1 = value.DeliveryLine1, line2 = value.DeliveryLine2, city = value.DeliveryCity, pincode = value.DeliveryPincode }, value.SellerOrders.Select(SellerOrderResponse.From).ToList());
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
