using System.Data;
using System.Security.Cryptography;
using Sanfiqo.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Sanfiqo.Identity;

public sealed class EfUserContactOwnershipStore : IUserContactOwnershipStore
{
    private readonly ApplicationIdentityDbContext _db;
    private readonly UserContactCryptography _cryptography;
    private readonly IEnumerable<IMobileAuthenticationWriteManifest> _writeManifests;

    public EfUserContactOwnershipStore(ApplicationIdentityDbContext db,
        UserContactCryptography cryptography,
        IEnumerable<IMobileAuthenticationWriteManifest>? writeManifests = null)
    {
        _db = db;
        _cryptography = cryptography;
        _writeManifests = writeManifests ?? Array.Empty<IMobileAuthenticationWriteManifest>();
    }

    public async Task<UserContactPendingWriteResult> AddPendingAsync(string userId,
        NormalizedUserContact contact, CancellationToken cancellationToken)
    {
        try
        {
            var contactId = Guid.NewGuid();
            _writeManifests.TrackBeforeWrite("UserContact", contactId.ToString("D"));
            _db.UserContacts.Add(new UserContact
            {
                Id = contactId,
                UserId = userId,
                Kind = contact.Kind,
                LookupKey = _cryptography.CreateLookupKey(contact),
                OwnershipKey = null,
                ProtectedValue = _cryptography.ProtectContactValue(contact.Value),
                IsVerified = false,
                VerifiedAtUtc = null,
                IsPrimary = false,
                CreatedAtUtc = DateTime.UtcNow,
                Version = 1
            });
            await _db.SaveChangesAsync(cancellationToken);
            return UserContactPendingWriteResult.Added;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return UserContactPendingWriteResult.PersistenceFailure;
        }
    }

    public async Task<UserContactClaimResult> ClaimVerifiedAsync(string userId,
        UserContactVerificationProof proof, CancellationToken cancellationToken)
    {
        var normalizedContact = new NormalizedUserContact(proof.Kind, proof.NormalizedValue);
        var lookupKey = _cryptography.CreateLookupKey(normalizedContact);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Serialize attempts that spend the same proof. The unique (Kind, OwnershipKey)
            // index remains the cross-proof/cross-account race arbiter.
            var proofRow = await _db.UserContactProofRecords.FromSqlInterpolated(
                    $"SELECT * FROM `UserContactVerificationProofs` WHERE `Id` = {proof.ProofId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow;
            if (proofRow is null || proofRow.SourceVerificationId != proof.SourceVerificationId
                || proofRow.Kind != proof.Kind || proofRow.Purpose != UserContactProofPurpose.Value
                || proofRow.ConsumedAtUtc is not null || proofRow.ExpiresAtUtc <= now
                || proofRow.IssuedAtUtc > now || !CryptographicOperations.FixedTimeEquals(proofRow.LookupKey, lookupKey))
            {
                await transaction.CommitAsync(cancellationToken);
                return UserContactClaimResult.InvalidProof;
            }

            var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return UserContactClaimResult.PersistenceFailure;
            }

            // Identity keeps its existing normalized-email index and columns. A matching
            // legacy Identity value for a different principal blocks a second owner.
            var identityConflict = proof.Kind switch
            {
                UserContactKind.Email => await _db.Users.AnyAsync(x => x.Id != userId
                    && x.NormalizedEmail == proof.NormalizedValue.ToUpperInvariant(), cancellationToken),
                UserContactKind.Mobile => await _db.Users.AnyAsync(x => x.Id != userId
                    && x.PhoneNumber == proof.NormalizedValue, cancellationToken),
                _ => true
            };

            var currentOwner = await _db.UserContacts.SingleOrDefaultAsync(x => x.Kind == proof.Kind
                && x.OwnershipKey == lookupKey, cancellationToken);
            var result = identityConflict || currentOwner is not null && currentOwner.UserId != userId
                ? UserContactClaimResult.OwnershipConflict
                : currentOwner is not null
                    ? UserContactClaimResult.AlreadyOwned
                    : UserContactClaimResult.Claimed;

            if (result == UserContactClaimResult.Claimed)
            {
                var pending = await _db.UserContacts.Where(x => x.UserId == userId
                    && x.Kind == proof.Kind && !x.IsVerified && x.LookupKey == lookupKey)
                    .OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
                if (pending is null)
                {
                    var contactId = Guid.NewGuid();
                    _writeManifests.TrackBeforeWrite("UserContact", contactId.ToString("D"));
                    _db.UserContacts.Add(new UserContact
                    {
                        Id = contactId,
                        UserId = userId,
                        Kind = proof.Kind,
                        LookupKey = lookupKey,
                        OwnershipKey = lookupKey,
                        ProtectedValue = _cryptography.ProtectContactValue(proof.NormalizedValue),
                        IsVerified = true,
                        VerifiedAtUtc = now,
                        IsPrimary = false,
                        CreatedAtUtc = now,
                        Version = 1
                    });
                }
                else
                {
                    pending.OwnershipKey = lookupKey;
                    pending.IsVerified = true;
                    pending.VerifiedAtUtc = now;
                    pending.Version++;
                }
            }

            // A valid proof is spent for success, idempotent same-owner claims, and conflicts.
            proofRow.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            if (IsMySqlDuplicateKey(exception))
                return UserContactClaimResult.OwnershipConflict;
            return UserContactClaimResult.PersistenceFailure;
        }
    }

    private static bool IsMySqlDuplicateKey(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is MySqlException { Number: 1062 }) return true;
        return false;
    }
}
