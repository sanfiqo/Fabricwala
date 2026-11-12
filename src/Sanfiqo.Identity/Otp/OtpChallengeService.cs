using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Sanfiqo.Identity;

namespace Sanfiqo.Identity;

public sealed class OtpChallengeService(
    IOtpChallengeStore store,
    IOtpDelivery delivery,
    OtpPolicy policy) : IOtpChallenges
{
    public async Task<Guid> RequestMobileCodeAsync(string mobileE164, string clientIp, CancellationToken cancellationToken = default)
    {
        if (!UserContactNormalizer.TryNormalize(UserContactKind.Mobile, mobileE164, out var normalizedMobile)
            || !IPAddress.TryParse(clientIp, out var parsedIp))
            throw new OtpRateLimitException();
        mobileE164 = normalizedMobile.Value;
        if (policy.Validate(requireIpLimits: true).Count != 0)
            throw new OtpDeliveryUnavailableException();

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow;
        var reservation = Guid.NewGuid();
        await store.ReserveAsync(reservation, mobileE164, parsedIp.ToString(), code, policy, now, cancellationToken);
        try { await delivery.SendAsync(reservation, mobileE164, code, now + policy.ChallengeLifetime, cancellationToken); }
        catch
        {
            try { await store.FailDeliveryAsync(reservation, DateTimeOffset.UtcNow, CancellationToken.None); }
            catch { /* Preserve the delivery failure and disclose no provider details. */ }
            throw new OtpDeliveryUnavailableException();
        }

        try
        {
            if (!await store.CompleteDeliveryAsync(reservation, DateTimeOffset.UtcNow, CancellationToken.None))
                throw new OtpDeliveryUnavailableException();
            return reservation;
        }
        catch
        {
            // Delivery already succeeded; retain its rate-limit event even if activation failed.
            throw new OtpDeliveryUnavailableException();
        }
    }

    public Task<OtpVerificationResult> VerifyAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty || code is null || code.Length != 6 || code.Any(c => c is < '0' or > '9'))
            return Task.FromResult(OtpVerificationResult.Invalid);
        return store.VerifyAsync(challengeId, code, policy.MaximumWrongAttempts, DateTimeOffset.UtcNow, cancellationToken);
    }

}
