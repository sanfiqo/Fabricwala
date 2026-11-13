using Sanfiqo.Identity;

namespace TextileConnect.Tests;

public sealed class OtpDevelopmentHelperTests
{
    [Fact]
    public void Fixed_code_is_used_only_in_development()
    {
        var policy = new OtpPolicy { DevFixedCode = "123456" };

        Assert.Equal("123456", policy.CreateCode("Development"));
        Assert.NotEqual("123456", policy.CreateCode("Production"));
    }

    [Fact]
    public void Invalid_fixed_code_falls_back_to_six_digit_code()
    {
        var policy = new OtpPolicy { DevFixedCode = "not-a-code" };

        var code = policy.CreateCode("Development");

        Assert.Matches("^[0-9]{6}$", code);
    }
}
