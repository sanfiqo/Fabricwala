using TextileConnect.Application.Catalogue;

namespace TextileConnect.Api;

public sealed record LandedCostResponse(long ProductId, int Qty, long CityId, decimal SlabPrice, decimal? ShippingPerUnit,
    decimal GstPerUnit, decimal PlatformFeePerUnit, decimal? LandedPerUnit, decimal? Total, int? TransitDays)
{
    public decimal FactoryTotal { get; init; }
    public decimal GstTotal { get; init; }
    public decimal? ShippingTotal { get; init; }
    public string ShippingStatus => ShippingPerUnit.HasValue ? "quoted" : "on_request";
    public static LandedCostResponse From(ProductQuote quote) => new(quote.ProductId, quote.Quantity, quote.CityId,
        quote.UnitPrice, quote.ShippingPerUnit, quote.GstPerUnit, 0m, quote.LandedPerUnit, quote.Total, quote.TransitDays)
        { FactoryTotal = quote.FactoryTotal, GstTotal = quote.GstTotal, ShippingTotal = quote.ShippingTotal };
}
