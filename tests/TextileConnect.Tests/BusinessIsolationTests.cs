using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Tests;

public sealed class BusinessIsolationTests
{
    [Fact]
    public void Directory_returns_only_the_requesting_users_business()
    {
        var directory = new InMemoryBusinessDirectory();
        directory.Create("user-a", NewBusiness("A"));
        directory.Create("user-b", NewBusiness("B"));

        Assert.Equal("A", directory.GetForUser("user-a")?.DisplayName);
        Assert.Null(directory.GetForUser("user-c"));
    }

    [Fact]
    public async Task Unapproved_business_does_not_satisfy_policy()
    {
        var handler = new ApprovedBusinessHandler();
        var context = new AuthorizationHandlerContext([new ApprovedBusinessRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity()), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void Price_slabs_require_moq_and_no_overlap()
    {
        var product = NewProduct(10);
        var errors = PriceSlabRules.Validate(product, [new PriceSlab { MinQty = 1, MaxQty = 20, PricePerUnit = 10 }, new PriceSlab { MinQty = 20, PricePerUnit = 9 }]);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Landed_cost_uses_freight_floor_gst_and_configured_fee()
    {
        var result = LandedCostCalculator.Calculate(300, 200, 0.05m, 0.5m,
            new ShippingRate { RateType = ShippingRateType.PerKg, Rate = 4, MinCharge = 4000 }, 0m);
        Assert.Equal(20m, result.FreightPerUnit);
        Assert.Equal(335m, result.TotalPerUnit);
    }

    private static Business NewBusiness(string name) => new()
    {
        LegalName = name,
        DisplayName = name,
        Phone = "+919876543210"
    };
    private static Product NewProduct(int moq) => new() { Title = "Test", Slug = "test", Moq = moq };
}
