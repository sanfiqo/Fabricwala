using Sanfiqo.Identity;

namespace Sanfiqo.Identity;

public enum UserContactClaimResult
{
    Claimed,
    AlreadyOwned,
    OwnershipConflict,
    InvalidProof,
    PersistenceFailure
}

public enum UserContactPendingWriteResult
{
    Added,
    InvalidInput,
    PersistenceFailure
}

/// <summary>A server-protected grant; clients submit only its opaque token.</summary>
public sealed record UserContactVerificationProof(
    Guid ProofId,
    Guid SourceVerificationId,
    UserContactKind Kind,
    string NormalizedValue,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Purpose);

public interface IUserContactOwnership
{
    Task<UserContactPendingWriteResult> AddPendingAsync(string userId, UserContactKind kind,
        string contactValue, CancellationToken cancellationToken = default);

    /// <summary>The only verified-claim entry point accepts a protected server proof, never a verified flag.</summary>
    Task<UserContactClaimResult> ClaimVerifiedAsync(string userId, string proofToken,
        CancellationToken cancellationToken = default);
}

public interface IUserContactVerificationProofIssuer
{
    /// <summary>
    /// Called only by trusted server code after the bound verification challenge has succeeded.
    /// The verification id is unique and cannot be used to issue a second proof.
    /// </summary>
    Task<string> IssueAfterTrustedVerificationAsync(UserContactKind kind, string contactValue,
        Guid successfulVerificationId, CancellationToken cancellationToken = default);
}

public interface IUserContactVerificationEvidence
{
    Task<bool> MatchesSuccessfulVerificationAsync(Guid sourceVerificationId, NormalizedUserContact contact,
        CancellationToken cancellationToken = default);
}

public interface IUserContactProofProtector
{
    bool TryUnprotect(string? token, out UserContactVerificationProof? proof);
}

public interface IUserContactOwnershipStore
{
    Task<UserContactPendingWriteResult> AddPendingAsync(string userId, NormalizedUserContact contact,
        CancellationToken cancellationToken);

    Task<UserContactClaimResult> ClaimVerifiedAsync(string userId, UserContactVerificationProof proof,
        CancellationToken cancellationToken);
}

public sealed class UserContactOwnershipService(
    IUserContactOwnershipStore store,
    IUserContactProofProtector proofProtector) : IUserContactOwnership
{
    public Task<UserContactPendingWriteResult> AddPendingAsync(string userId, UserContactKind kind,
        string contactValue, CancellationToken cancellationToken = default)
    {
        if (!ValidUserId(userId) || !UserContactNormalizer.TryNormalize(kind, contactValue, out var contact))
            return Task.FromResult(UserContactPendingWriteResult.InvalidInput);
        return store.AddPendingAsync(userId, contact, cancellationToken);
    }

    public Task<UserContactClaimResult> ClaimVerifiedAsync(string userId, string proofToken,
        CancellationToken cancellationToken = default)
    {
        if (!ValidUserId(userId) || !proofProtector.TryUnprotect(proofToken, out var proof) || proof is null
            || proof.ProofId == Guid.Empty || proof.SourceVerificationId == Guid.Empty
            || proof.Purpose != UserContactProofPurpose.Value
            || proof.IssuedAtUtc > DateTimeOffset.UtcNow || proof.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || !UserContactNormalizer.TryNormalize(proof.Kind, proof.NormalizedValue, out var contact)
            || !string.Equals(contact.Value, proof.NormalizedValue, StringComparison.Ordinal))
            return Task.FromResult(UserContactClaimResult.InvalidProof);
        return store.ClaimVerifiedAsync(userId, proof, cancellationToken);
    }

    private static bool ValidUserId(string userId) => !string.IsNullOrWhiteSpace(userId) && userId.Length <= 128;
}

public static class UserContactProofPurpose
{
    public const string Value = "user-contact-ownership-v1";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
}
