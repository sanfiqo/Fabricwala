using System.Data;
using System.Security.Cryptography;
using Sanfiqo.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace Sanfiqo.Identity;

/// <summary>Owns the atomic verified-mobile resolution, proof consumption and user creation.</summary>
public sealed class EfVerifiedMobileAccountStore : IVerifiedMobileAccountStore
{
    private const string UserRole = SanfiqoRoleNames.Retailer;
    private readonly ApplicationIdentityDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserContactCryptography _cryptography;
    private readonly IUserContactProofProtector _proofProtector;
    private readonly IEnumerable<IMobileAuthenticationWriteManifest> _writeManifests;
    private readonly IConfiguration _configuration;

    public EfVerifiedMobileAccountStore(ApplicationIdentityDbContext db,
        UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles,
        UserContactCryptography cryptography, IUserContactProofProtector proofProtector,
        IEnumerable<IMobileAuthenticationWriteManifest>? writeManifests = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _cryptography = cryptography;
        _proofProtector = proofProtector;
        _writeManifests = writeManifests ?? Array.Empty<IMobileAuthenticationWriteManifest>();
        _configuration = configuration ?? new ConfigurationBuilder().Build();
    }

    public async Task<VerifiedMobileAccountResult> CompleteAsync(NormalizedUserContact mobile,
        string trustedProofToken, CancellationToken cancellationToken = default)
    {
        if (mobile.Kind != UserContactKind.Mobile
            || !UserContactNormalizer.TryNormalize(UserContactKind.Mobile, mobile.Value, out var canonical)
            || canonical.Value != mobile.Value
            || !_proofProtector.TryUnprotect(trustedProofToken, out var proof) || proof is null
            || proof.Kind != UserContactKind.Mobile || proof.NormalizedValue != mobile.Value
            || proof.Purpose != UserContactProofPurpose.Value || proof.ProofId == Guid.Empty
            || proof.SourceVerificationId == Guid.Empty || proof.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || proof.IssuedAtUtc > DateTimeOffset.UtcNow)
            return new(VerifiedMobileAccountStatus.Rejected);

        var ownerKey = _cryptography.CreateLookupKey(mobile);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        try
        {
            // Lock the nonce before looking up/creating an account. The unique verified-owner
            // index arbitrates simultaneous different proofs for the same mobile.
            var proofRow = await _db.UserContactProofRecords.FromSqlInterpolated(
                    $"SELECT * FROM `UserContactVerificationProofs` WHERE `Id` = {proof.ProofId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow;
            if (proofRow is null || proofRow.SourceVerificationId != proof.SourceVerificationId
                || proofRow.Kind != UserContactKind.Mobile
                || proofRow.Purpose != UserContactProofPurpose.Value
                || proofRow.ConsumedAtUtc is not null || proofRow.ExpiresAtUtc <= now
                || proofRow.IssuedAtUtc > now
                || !CryptographicOperations.FixedTimeEquals(proofRow.LookupKey, ownerKey))
            {
                await transaction.CommitAsync(cancellationToken);
                return new(VerifiedMobileAccountStatus.Rejected);
            }

            // Identity PhoneNumber is deliberately not an owner lookup. A legacy/unverified
            // match blocks creation, but can never authenticate or select an account.
            var owner = await _db.UserContacts.SingleOrDefaultAsync(x => x.Kind == UserContactKind.Mobile
                && x.OwnershipKey == ownerKey, cancellationToken);
            if (owner is not null && (!owner.IsVerified || owner.VerifiedAtUtc is null
                || owner.LookupKey.Length != ownerKey.Length
                || !CryptographicOperations.FixedTimeEquals(owner.LookupKey, ownerKey)))
            {
                proofRow.ConsumedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(VerifiedMobileAccountStatus.Rejected);
            }

            if (owner is not null)
            {
                var user = await _users.FindByIdAsync(owner.UserId);
                IList<string> userRoles = user is null ? Array.Empty<string>() : await _users.GetRolesAsync(user);
                var duplicateLegacyPhone = user is not null && await _db.Users.AnyAsync(x => x.Id != user.Id
                    && x.PhoneNumber == mobile.Value, cancellationToken);
                proofRow.ConsumedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // OTP alone must never enter manufacturer/admin scope. Existing users must
                // already be active; no legacy activation or role promotion is inferred.
                var existingBootstrapAdmin = IsBootstrapAdmin(mobile.Value);
                if (user is null || !user.IsActive || duplicateLegacyPhone
                    || (!existingBootstrapAdmin && (userRoles.Count != 1 || !userRoles.Contains(UserRole, StringComparer.Ordinal)))
                    || (existingBootstrapAdmin && !userRoles.Contains(SanfiqoRoleNames.Admin, StringComparer.Ordinal)))
                    return new(VerifiedMobileAccountStatus.Rejected);
                return new(VerifiedMobileAccountStatus.ExistingUser, user.Id);
            }

            if (await _db.Users.AnyAsync(x => x.PhoneNumber == mobile.Value, cancellationToken))
            {
                proofRow.ConsumedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(VerifiedMobileAccountStatus.Rejected);
            }

            var bootstrapAdmin = IsBootstrapAdmin(mobile.Value);
            var roleName = bootstrapAdmin ? SanfiqoRoleNames.Admin : UserRole;
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null)
            {
                var userRole = new ApplicationRole { Id = Guid.NewGuid().ToString("D"), Name = roleName };
                _writeManifests.TrackBeforeWrite("SharedRole", userRole.Id);
                var createRole = await _roles.CreateAsync(userRole);
                if (!createRole.Succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _db.ChangeTracker.Clear();
                    return new(VerifiedMobileAccountStatus.Unavailable);
                }
            }

            var newUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("D"),
                UserName = $"user-{Guid.NewGuid():N}",
                PhoneNumber = mobile.Value,
                PhoneNumberConfirmed = true,
                IsActive = false
            };
            _writeManifests.TrackBeforeWrite("ApplicationUser", newUser.Id);
            var createUser = await _users.CreateAsync(newUser);
            if (!createUser.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return new(VerifiedMobileAccountStatus.Unavailable);
            }

            var addRole = await _users.AddToRoleAsync(newUser, roleName);
            if (!addRole.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return new(VerifiedMobileAccountStatus.Unavailable);
            }

            var contactId = Guid.NewGuid();
            _writeManifests.TrackBeforeWrite("UserContact", contactId.ToString("D"));
            _db.UserContacts.Add(new UserContact
            {
                Id = contactId,
                UserId = newUser.Id,
                Kind = UserContactKind.Mobile,
                LookupKey = ownerKey,
                OwnershipKey = ownerKey,
                ProtectedValue = _cryptography.ProtectContactValue(mobile.Value),
                IsVerified = true,
                VerifiedAtUtc = now,
                IsPrimary = true,
                CreatedAtUtc = now,
                Version = 1
            });
            // Activate only after the verified owner row and User role are part of this
            // same transaction. IsActive's schema default remains false on every other path.
            newUser.IsActive = true;
            proofRow.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(VerifiedMobileAccountStatus.RegisteredUser, newUser.Id);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return new(IsDuplicateKey(exception)
                ? VerifiedMobileAccountStatus.Rejected
                : VerifiedMobileAccountStatus.Unavailable);
        }
    }

    private bool IsBootstrapAdmin(string mobile)
        => (_configuration.GetSection("Bootstrap:AdminPhones").Get<string[]>() ?? Array.Empty<string>())
            .Any(phone => UserContactNormalizer.TryNormalize(UserContactKind.Mobile, phone, out var normalized)
                && normalized.Value == mobile);

    private static bool IsDuplicateKey(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is MySqlException { Number: 1062 }) return true;
        return false;
    }
}
