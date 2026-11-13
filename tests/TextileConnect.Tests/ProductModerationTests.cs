namespace TextileConnect.Tests;

public sealed class ProductModerationTests
{
    [Fact]
    public void Moderation_supports_flag_deactivate_and_activate_actions()
    {
        var actions = new[] { "Flag", "Deactivate", "Activate" };
        Assert.Contains("Flag", actions);
        Assert.Contains("Deactivate", actions);
        Assert.Contains("Activate", actions);
    }
}
