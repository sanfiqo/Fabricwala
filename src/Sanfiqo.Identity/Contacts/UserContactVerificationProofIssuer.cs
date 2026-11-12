using Sanfiqo.Identity;
using Microsoft.EntityFrameworkCore;

namespace Sanfiqo.Identity;

/// <summary>Server-side adapter. Only trusted verification workflows may call this issuer.</summary>
public sealed class UserContactVerificationProofIssuer(
    ApplicationIdentityDbContext db,
    UserContactCryptography cryptography,
    IUserContactVerificationEvidence evidence,
    IEnumerable<IMobileAuthenticationWriteManifest>? writeManifests = null) : IUserContactVerificationProofIssuer
{
    private readonly IEnumerable<IMobileAuthenticationWriteManifest> _writeManifests =
        writeManifests ?? Array.Empty<IMobileAuthenticationWriteManifest>();

    public async Task<string> IssueAfterTrustedVerificationAsync(UserContactKind kind, string contactValue,
        Guid successfulVerificationId, CancellationToken cancellationToken = default)
    {
        if (successfulVerificationId == Guid.Empty
            || !UserContactNormalizer.TryNormalize(kind, contactValue, out var contact))
            throw new InvalidOperationException("A trusted user contact verification is required.");
        if (!await evidence.MatchesSuccessfulVerificationAsync(successfulVerificationId, contact, cancellationToken))
            throw new InvalidOperationException("A trusted user contact verification is required.");

        var issued = DateTimeOffset.UtcNow;
        var proof = new UserContactVerificationProof(Guid.NewGuid(), successfulVerificationId,
            kind, contact.Value, issued, issued + UserContactProofPurpose.Lifetime, UserContactProofPurpose.Value);
        var token = cryptography.Protect(proof);
        _writeManifests.TrackBeforeWrite("UserContactVerificationProof", proof.ProofId.ToString("D"));
        db.UserContactProofRecords.Add(new UserContactProofRecord
        {
            Id = proof.ProofId,
            SourceVerificationId = successfulVerificationId,
            Kind = kind,
            LookupKey = cryptography.CreateLookupKey(contact),
            IssuedAtUtc = issued.UtcDateTime,
            ExpiresAtUtc = proof.ExpiresAtUtc.UtcDateTime,
            ConsumedAtUtc = null,
            Purpose = UserContactProofPurpose.Value
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("The user contact verification proof could not be issued.");
        }
        return token;
    }
}
