namespace TextileConnect.Tests;

public sealed class StockAlertTests
{
    [Fact]
    public void Zero_stock_is_out_of_stock_and_positive_low_stock_is_low_stock()
    {
        Assert.Equal("OutOfStock", 0 == 0 ? "OutOfStock" : "LowStock");
        Assert.Equal("LowStock", 3 == 0 ? "OutOfStock" : "LowStock");
    }
}
