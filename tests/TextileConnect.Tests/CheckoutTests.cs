using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;

namespace TextileConnect.Tests;

public sealed class CheckoutTests
{
    [Fact]
    public void Checkout_splits_sellers_and_uses_two_percent_manufacturer_commission()
    {
        var split = CheckoutRules.SplitBySeller([(1, 100m), (2, 50m), (1, 25m)]);
        Assert.Equal(125m, split[1]);
        Assert.Equal(2m, CheckoutRules.Commission(100m, null, new PlatformSettings()));
    }

    [Fact]
    public void Checkout_rejects_a_tampered_client_price()
    {
        Assert.False(CheckoutRules.IsClientPriceValid(1m, 2m));
        Assert.True(CheckoutRules.IsClientPriceValid(2m, 2m));
    }
}
