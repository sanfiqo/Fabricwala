using Sanfiqo.Identity;

namespace Sanfiqo.Identity;

public sealed class UserContact
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public UserContactKind Kind { get; set; }
    public required byte[] LookupKey { get; set; }
    public byte[]? OwnershipKey { get; set; }
    public required byte[] ProtectedValue { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public ApplicationUser? User { get; set; }
}

/// <summary>Durable nonce and contact binding for a protected, expiring, single-use proof.</summary>
public sealed class UserContactProofRecord
{
    public Guid Id { get; set; }
    public Guid SourceVerificationId { get; set; }
    public UserContactKind Kind { get; set; }
    public required byte[] LookupKey { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public required string Purpose { get; set; }
}
