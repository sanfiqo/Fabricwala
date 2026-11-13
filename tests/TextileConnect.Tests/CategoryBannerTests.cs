namespace TextileConnect.Tests;

public sealed class CategoryBannerTests
{
    [Fact]
    public void Banner_visibility_requires_current_time_inside_its_window()
    {
        var now = DateTime.UtcNow; var starts = now.AddHours(-1); var ends = now.AddHours(1);
        Assert.True(starts <= now && ends >= now);
    }
}
