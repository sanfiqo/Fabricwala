namespace TextileConnect.Application.Catalogue;

public sealed record ProductQuote(long ProductId, int Quantity, long CityId, decimal UnitPrice, decimal FactoryTotal,
    decimal GstTotal, decimal? ShippingTotal, int? TransitDays)
{
    public decimal GstPerUnit => Money(GstTotal / Quantity);
    public decimal? ShippingPerUnit => ShippingTotal.HasValue ? Money(ShippingTotal.Value / Quantity) : null;
    public decimal? Total => ShippingTotal.HasValue ? FactoryTotal + GstTotal + ShippingTotal.Value : null;
    public decimal? LandedPerUnit => Total.HasValue ? Money(Total.Value / Quantity) : null;
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
