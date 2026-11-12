using System.Data;
using System.Security.Cryptography;
using System.Text;
using Sanfiqo.Identity;
using Microsoft.EntityFrameworkCore;

namespace Sanfiqo.Identity;

public sealed class EfOtpChallengeStore : IOtpChallengeStore
{
    private const byte Pending = 0, Active = 1, Consumed = 2, Exhausted = 3, DeliveryFailed = 4, Verified = 5;
    private readonly ApplicationIdentityDbContext _db;
    private readonly OtpPolicy _policy;

    public EfOtpChallengeStore(ApplicationIdentityDbContext db, OtpPolicy policy)
    {
        _db = db;
        _policy = policy;
    }

    public async Task ReserveAsync(Guid id, string mobileE164, string clientIp, string code, OtpPolicy policy,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || policy.Validate(requireIpLimits: true).Count != 0)
            throw new OtpDeliveryUnavailableException();
        var utcNow = now.UtcDateTime;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockOtpWritesAsync(cancellationToken);

        var retentionStart = utcNow.AddHours(-24);
        await _db.OtpSendEvents.Where(x => x.OccurredAtUtc < retentionStart).ExecuteDeleteAsync(cancellationToken);
        await _db.OtpChallenges.Where(x => x.CreatedAtUtc < retentionStart).ExecuteDeleteAsync(cancellationToken);

        var events = await _db.OtpSendEvents.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= retentionStart).ToListAsync(cancellationToken);
        var mobileHash = _policy.Hash(mobileE164);
        var ipHash = _policy.Hash(clientIp);
        var mobileEvents = events.Where(x => FixedEquals(x.MobileHash, mobileHash))
            .OrderByDescending(x => x.OccurredAtUtc).ToArray();
        var ipEvents = events.Where(x => FixedEquals(x.IpHash, ipHash)).ToArray();
        var mobile24HourCount = mobileEvents.Count(x => x.OccurredAtUtc > retentionStart);
        var ip24HourCount = ipEvents.Count(x => x.OccurredAtUtc > retentionStart);
        if (mobileEvents.Length > 0 && mobileEvents[0].OccurredAtUtc > utcNow - policy.ResendCooldown
            || mobileEvents.Count(x => x.OccurredAtUtc > utcNow.AddMinutes(-15)) >= policy.MobileSendsPer15Minutes
            || mobile24HourCount >= policy.MobileSendsPer24Hours
            || ipEvents.Count(x => x.OccurredAtUtc > utcNow.AddMinutes(-15)) >= policy.IpSendsPer15Minutes
            || ip24HourCount >= policy.IpSendsPer24Hours)
            throw new OtpRateLimitException();

        _db.OtpSendEvents.Add(new OtpSendEvent
        {
            ChallengeId = id,
            MobileHash = mobileHash,
            IpHash = ipHash,
            OccurredAtUtc = utcNow
        });
        _db.OtpChallenges.Add(new OtpChallenge
        {
            Id = id,
            MobileHash = mobileHash,
            CodeHash = _policy.Hash(code),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow + policy.ChallengeLifetime,
            State = Pending
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> CompleteDeliveryAsync(Guid reservationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var utcNow = now.UtcDateTime;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockOtpWritesAsync(cancellationToken);
        var reservation = await _db.OtpChallenges.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        var sendEvent = await _db.OtpSendEvents.SingleOrDefaultAsync(x => x.ChallengeId == reservationId, cancellationToken);
        if (sendEvent is not null) sendEvent.OccurredAtUtc = utcNow;
        if (reservation is null || reservation.State != Pending || reservation.ExpiresAtUtc <= utcNow)
        {
            if (sendEvent is not null) await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var mobileHash = reservation.MobileHash;
        var pending = await _db.OtpChallenges.Where(x => x.State == Pending && x.Id != reservationId)
            .ToListAsync(cancellationToken);
        if (pending.Any(x => FixedEquals(x.MobileHash, mobileHash)
            && (x.CreatedAtUtc > reservation.CreatedAtUtc || x.CreatedAtUtc == reservation.CreatedAtUtc && x.Id.CompareTo(reservation.Id) > 0)))
        {
            reservation.State = DeliveryFailed;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var active = await _db.OtpChallenges.Where(x => x.State == Active).ToListAsync(cancellationToken);
        foreach (var oldChallenge in active.Where(x => FixedEquals(x.MobileHash, mobileHash)))
        {
            oldChallenge.State = Consumed;
            oldChallenge.ConsumedAtUtc = utcNow;
        }
        reservation.State = Active;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task FailDeliveryAsync(Guid reservationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockOtpWritesAsync(cancellationToken);
        var reservation = await _db.OtpChallenges.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation is not null && reservation.State == Pending)
        {
            reservation.State = DeliveryFailed;
            // Keep the reservation in rate accounting: a provider exception can follow an
            // accepted send, and deleting it could bypass limits or invite blind retries.
            var sendEvent = await _db.OtpSendEvents.SingleOrDefaultAsync(x => x.ChallengeId == reservationId, cancellationToken);
            if (sendEvent is not null) sendEvent.OccurredAtUtc = now.UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<OtpVerificationResult> VerifyAsync(Guid challengeId, string code, int maximumWrongAttempts,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockOtpWritesAsync(cancellationToken);
        var challenge = await _db.OtpChallenges.SingleOrDefaultAsync(x => x.Id == challengeId, cancellationToken);
        if (challenge is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.NotFound;
        }
        if (challenge.State is Consumed or Verified)
        {
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.AlreadyConsumed;
        }
        if (challenge.State == Exhausted || challenge.WrongAttempts >= maximumWrongAttempts)
        {
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.AttemptsExhausted;
        }
        if (challenge.State != Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.Invalid;
        }
        if (challenge.ExpiresAtUtc <= now.UtcDateTime)
        {
            challenge.State = Consumed;
            challenge.ConsumedAtUtc = now.UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.Expired;
        }

        var valid = FixedEquals(_policy.Hash(code), challenge.CodeHash);
        if (valid)
        {
            challenge.State = Verified;
            challenge.ConsumedAtUtc = now.UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OtpVerificationResult.Accepted;
        }

        challenge.WrongAttempts++;
        if (challenge.WrongAttempts >= maximumWrongAttempts) challenge.State = Exhausted;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return challenge.State == Exhausted ? OtpVerificationResult.AttemptsExhausted : OtpVerificationResult.Invalid;
    }

    private async Task LockOtpWritesAsync(CancellationToken cancellationToken)
    {
        // The migration seeds this gate. A row lock serializes rate admission, activation and consume.
        await _db.OtpWriteGates.FromSqlRaw(
            "SELECT * FROM `OtpWriteGates` WHERE `Id` = 1 FOR UPDATE")
            .SingleAsync(cancellationToken);
    }

    private static bool FixedEquals(byte[] left, byte[] right)
        => left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}
