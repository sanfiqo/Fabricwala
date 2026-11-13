using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class LandedCostTests
{
    [Fact]
    public void Missing_rate_is_shipping_on_request()
    {
        var quote = new LandedCostResponse(1, 10, 2, 100m, null, 5m, 0m, null, null, null);
        Assert.Null(quote.ShippingPerUnit);
        Assert.Null(quote.LandedPerUnit);
        Assert.Null(quote.Total);
    }
}
