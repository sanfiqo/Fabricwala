using Sanfiqo.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TextileConnect.Tests;

public sealed class OtpDevelopmentHelperTests
{
    [Theory]
    [InlineData("Development", "123456")]
    [InlineData("Production", null)]
    public void Registration_never_relaxes_security_limits(string environment, string? expectedCode)
    {
        var services = new ServiceCollection();
        services.AddOtpChallengeFoundation(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:DevFixedCode"] = "654321", ["Otp:Policy:RelaxRateLimits"] = "true"
        }).Build(), environment);
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<OtpPolicy>();
        Assert.Equal(expectedCode, policy.DevFixedCode);
        Assert.False(policy.RelaxRateLimits);
        Assert.Equal(5, policy.MaximumWrongAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.ChallengeLifetime);
        Assert.Equal(TimeSpan.FromSeconds(60), policy.ResendCooldown);
    }

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
