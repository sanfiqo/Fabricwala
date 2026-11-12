using Sanfiqo.Identity;

namespace TextileConnect.Tests;

public sealed class IdentityTests
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

    [Fact]
    public async Task MobileAuthenticationService_accepts_verified_code_and_returns_account()
    {
        var challengeId = Guid.NewGuid();
        var challenges = new TestOtpChallenges(challengeId, OtpVerificationResult.Accepted);
        var service = new MobileAuthenticationService(challenges, new TestProofIssuer(), new TestAccountStore("user-1"));
        var result = await service.CompleteMobileSignInAsync(challengeId, "+91 98765 43210", "123456");
        Assert.Equal(MobileAuthenticationStatus.Authenticated, result.Status);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal(1, challenges.VerificationCount);
    }

    [Fact]
    public async Task MobileAuthenticationService_rejects_all_five_wrong_attempts_and_locked_attempt()
    {
        var challengeId = Guid.NewGuid();
        var challenges = new TestOtpChallenges(challengeId, OtpVerificationResult.AttemptsExhausted);
        var service = new MobileAuthenticationService(challenges, new TestProofIssuer(), new TestAccountStore("user-1"));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await service.CompleteMobileSignInAsync(challengeId, "+919876543210", "000000");
            Assert.Equal(MobileAuthenticationStatus.Rejected, result.Status);
        }
        Assert.Equal(5, challenges.VerificationCount);
    }

    private sealed class TestOtpChallenges(Guid expectedId, OtpVerificationResult result) : IOtpChallenges
    {
        public int VerificationCount { get; private set; }
        public Task<Guid> RequestMobileCodeAsync(string mobileE164, string clientIp, CancellationToken cancellationToken = default) => Task.FromResult(expectedId);
        public Task<OtpVerificationResult> VerifyAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
        { VerificationCount++; return Task.FromResult(challengeId == expectedId ? result : OtpVerificationResult.NotFound); }
    }

    private sealed class TestProofIssuer : IUserContactVerificationProofIssuer
    {
        public Task<string> IssueAfterTrustedVerificationAsync(UserContactKind kind, string normalizedValue, Guid sourceVerificationId, CancellationToken cancellationToken = default) => Task.FromResult("trusted-proof");
    }

    private sealed class TestAccountStore(string userId) : IVerifiedMobileAccountStore
    {
        public Task<VerifiedMobileAccountResult> CompleteAsync(NormalizedUserContact mobile, string trustedProofToken, CancellationToken cancellationToken = default) => Task.FromResult(new VerifiedMobileAccountResult(VerifiedMobileAccountStatus.ExistingUser, userId));
    }
}
