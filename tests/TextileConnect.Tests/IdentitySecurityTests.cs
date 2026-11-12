using Sanfiqo.Identity;

namespace TextileConnect.Tests;

public sealed class IdentitySecurityTests
{
    [Fact]
    public void OtpPolicy_requires_both_ip_limits_for_issuance()
    {
        var errors = new OtpPolicy().Validate(requireIpLimits: true);

        Assert.Contains(errors, error => error.Contains("IP send limits", StringComparison.Ordinal));
    }

    [Fact]
    public void OtpPolicy_rejects_changes_to_security_constants()
    {
        var policy = new OtpPolicy { MaximumWrongAttempts = 4 };

        Assert.Contains(policy.Validate(), error => error.Contains("MaximumWrongAttempts", StringComparison.Ordinal));
    }

    [Fact]
    public void UserContactNormalizer_canonicalizes_mobile()
    {
        var valid = UserContactNormalizer.TryNormalize(UserContactKind.Mobile, " +91 (98765) 43210 ", out var contact);

        Assert.True(valid);
        Assert.Equal("+919876543210", contact.Value);
    }

    [Fact]
    public async Task FakeOtpDelivery_exposes_code_only_to_local_inspection()
    {
        var delivery = new FakeOtpDelivery();
        var id = Guid.NewGuid();

        await delivery.SendAsync(id, "+919876543210", "123456", DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.True(delivery.TryGetCode(id, out var code));
        Assert.Equal("123456", code);
    }
}
