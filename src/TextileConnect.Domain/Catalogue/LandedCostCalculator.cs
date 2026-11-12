namespace TextileConnect.Domain.Catalogue;

public sealed record LandedCost(decimal SlabPrice, decimal FreightPerUnit, decimal GstPerUnit, decimal PlatformFeePerUnit)
{
    public decimal TotalPerUnit => SlabPrice + FreightPerUnit + GstPerUnit + PlatformFeePerUnit;
}

public static class LandedCostCalculator
{
    public static LandedCost Calculate(decimal slabPrice, int quantity, decimal gstRate, decimal weightPerUnitKg,
        ShippingRate rate, decimal platformFeeRate)
    {
        var freight = rate.RateType == ShippingRateType.PerKg
            ? Math.Max(rate.Rate * weightPerUnitKg * quantity, rate.MinCharge) / quantity
            : rate.Rate;
        return new(slabPrice, freight, slabPrice * gstRate, slabPrice * platformFeeRate);
    }
}
