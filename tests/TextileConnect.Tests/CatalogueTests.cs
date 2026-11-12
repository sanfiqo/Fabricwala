using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Tests;

public sealed class CatalogueTests
{
    [Fact]
    public void Price_slabs_require_moq_and_no_overlap()
    {
        var product = new Product { Title = "Test", Slug = "test", Moq = 10 };
        var errors = PriceSlabRules.Validate(product, [new PriceSlab { MinQty = 1, MaxQty = 20, PricePerUnit = 10 }, new PriceSlab { MinQty = 20, PricePerUnit = 9 }]);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Slab_selection_chooses_the_highest_matching_minimum()
    {
        var selected = PriceSlabRules.Select([
            new PriceSlab { MinQty = 10, MaxQty = 49, PricePerUnit = 20 },
            new PriceSlab { MinQty = 50, PricePerUnit = 18 }], 75);

        Assert.Equal(18m, selected?.PricePerUnit);
    }

    [Fact]
    public void Landed_cost_uses_freight_floor_gst_and_configured_fee()
    {
        var result = LandedCostCalculator.Calculate(300, 200, 0.05m, 0.5m,
            new ShippingRate { RateType = ShippingRateType.PerKg, Rate = 4, MinCharge = 4000 }, 0m);
        Assert.Equal(20m, result.FreightPerUnit);
        Assert.Equal(335m, result.TotalPerUnit);
    }
}
