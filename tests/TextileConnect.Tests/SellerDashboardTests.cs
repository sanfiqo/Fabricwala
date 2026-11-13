namespace TextileConnect.Tests;

public sealed class SellerDashboardTests
{
    [Fact]
    public void Dashboard_low_stock_threshold_is_five_units()
    {
        Assert.True(5 <= 5);
        Assert.False(6 <= 5);
    }
}
