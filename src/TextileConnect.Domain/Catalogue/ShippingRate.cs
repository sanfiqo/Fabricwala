namespace TextileConnect.Domain.Catalogue;

public enum ShippingRateType { PerKg, PerPiece }

public sealed class ShippingRate
{
    public long FromCityId { get; set; }
    public long ToCityId { get; set; }
    public ShippingRateType RateType { get; set; }
    public decimal Rate { get; set; }
    public decimal MinCharge { get; set; }
    public int TransitDays { get; set; }
    public bool IsActive { get; set; } = true;
}
