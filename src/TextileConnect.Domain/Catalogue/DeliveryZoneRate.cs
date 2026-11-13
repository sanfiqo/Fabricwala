namespace TextileConnect.Domain.Catalogue;

public sealed class DeliveryZoneRate
{
    public long FromZoneId { get; set; }
    public long ToZoneId { get; set; }
    public ShippingRateType RateType { get; set; }
    public decimal Rate { get; set; }
    public decimal MinCharge { get; set; }
    public int TransitDays { get; set; }
    public bool IsActive { get; set; } = true;
}
