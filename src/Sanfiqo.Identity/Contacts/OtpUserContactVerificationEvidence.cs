using System.Security.Cryptography;
using Sanfiqo.Identity;
using Microsoft.EntityFrameworkCore;

namespace Sanfiqo.Identity;

/// <summary>Accepts proof source IDs only for a successfully consumed OTP bound to the same mobile.</summary>
public sealed class OtpUserContactVerificationEvidence : IUserContactVerificationEvidence
{
    private readonly ApplicationIdentityDbContext _db;
    private readonly OtpPolicy _policy;

    public OtpUserContactVerificationEvidence(ApplicationIdentityDbContext db, OtpPolicy policy)
    {
        _db = db;
        _policy = policy;
    }

    public async Task<bool> MatchesSuccessfulVerificationAsync(Guid sourceVerificationId,
        NormalizedUserContact contact, CancellationToken cancellationToken = default)
    {
        // No email verifier/provider is active. Email proofs fail closed until a trusted adapter exists.
        if (sourceVerificationId == Guid.Empty || contact.Kind != UserContactKind.Mobile) return false;
        var challenge = await _db.OtpChallenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sourceVerificationId && x.State == 5, cancellationToken);
        if (challenge is null) return false;
        var expected = _policy.Hash(contact.Value);
        return CryptographicOperations.FixedTimeEquals(challenge.MobileHash, expected);
    }
}
