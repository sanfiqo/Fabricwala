using Sanfiqo.Identity;

namespace Sanfiqo.Identity;

public enum MobileAuthenticationStatus
{
    Authenticated,
    Rejected,
    Unavailable
}

public sealed record MobileAuthenticationResult(MobileAuthenticationStatus Status, string? UserId = null);

public enum VerifiedMobileAccountStatus
{
    ExistingUser,
    RegisteredUser,
    Rejected,
    Unavailable
}

public sealed record VerifiedMobileAccountResult(VerifiedMobileAccountStatus Status, string? UserId = null);

public interface IMobileAuthentication
{
    Task<Guid> RequestMobileCodeAsync(string mobile, string clientIp, CancellationToken cancellationToken = default);
    Task<MobileAuthenticationResult> CompleteMobileSignInAsync(Guid challengeId, string mobile, string code,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves only verified UserContacts by their keyed ownership identifier. Implementations
/// consume the trusted proof and commit the complete account/ownership mutation before returning
/// an authenticated user id.
/// </summary>
public interface IVerifiedMobileAccountStore
{
    Task<VerifiedMobileAccountResult> CompleteAsync(NormalizedUserContact mobile, string trustedProofToken,
        CancellationToken cancellationToken = default);
}

public sealed class MobileAuthenticationService(
    IOtpChallenges challenges,
    IUserContactVerificationProofIssuer proofs,
    IVerifiedMobileAccountStore accounts) : IMobileAuthentication
{
    public Task<Guid> RequestMobileCodeAsync(string mobile, string clientIp,
        CancellationToken cancellationToken = default)
        => challenges.RequestMobileCodeAsync(mobile, clientIp, cancellationToken);

    public async Task<MobileAuthenticationResult> CompleteMobileSignInAsync(Guid challengeId, string mobile,
        string code, CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty || string.IsNullOrWhiteSpace(code)
            || !UserContactNormalizer.TryNormalize(UserContactKind.Mobile, mobile, out var normalized))
            return new(MobileAuthenticationStatus.Rejected);

        var verification = await challenges.VerifyAsync(challengeId, code, cancellationToken);
        if (verification != OtpVerificationResult.Accepted)
            return new(MobileAuthenticationStatus.Rejected);

        string proof;
        try
        {
            proof = await proofs.IssueAfterTrustedVerificationAsync(UserContactKind.Mobile,
                normalized.Value, challengeId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new(MobileAuthenticationStatus.Rejected);
        }

        var account = await accounts.CompleteAsync(normalized, proof, cancellationToken);
        return account.Status switch
        {
            VerifiedMobileAccountStatus.ExistingUser or VerifiedMobileAccountStatus.RegisteredUser
                when !string.IsNullOrWhiteSpace(account.UserId)
                => new(MobileAuthenticationStatus.Authenticated, account.UserId),
            VerifiedMobileAccountStatus.Unavailable => new(MobileAuthenticationStatus.Unavailable),
            _ => new(MobileAuthenticationStatus.Rejected)
        };
    }
}
