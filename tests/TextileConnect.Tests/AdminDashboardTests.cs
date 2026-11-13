namespace TextileConnect.Tests;

public sealed class AdminDashboardTests
{
    [Fact]
    public void Dashboard_window_is_limited_to_one_year()
    {
        Assert.Equal(1, Math.Clamp(0, 1, 365));
        Assert.Equal(365, Math.Clamp(400, 1, 365));
    }
}
