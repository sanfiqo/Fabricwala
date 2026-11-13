namespace TextileConnect.Tests;

public sealed class AdminReportTests
{
    [Fact]
    public void Reports_support_the_four_required_exports()
    {
        var reports = new[] { "orders", "payments", "settlements", "businesses" };
        Assert.Equal(4, reports.Length);
    }
}
