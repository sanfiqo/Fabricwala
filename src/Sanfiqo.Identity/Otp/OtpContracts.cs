namespace Sanfiqo.Identity;

public enum OtpVerificationResult
{
    Accepted,
    Invalid,
    Expired,
    AttemptsExhausted,
    AlreadyConsumed,
    NotFound
}

public interface IOtpChallenges
{
    Task<Guid> RequestMobileCodeAsync(string mobileE164, string clientIp, CancellationToken cancellationToken = default);
    Task<OtpVerificationResult> VerifyAsync(Guid challengeId, string code, CancellationToken cancellationToken = default);
}

/// <summary>Only challenge material is returned. The code and destination are never returned.</summary>
public interface IOtpChallengeStore
{
    Task ReserveAsync(Guid challengeId, string mobileE164, string clientIp, string code, OtpPolicy policy,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> CompleteDeliveryAsync(Guid reservationId, DateTimeOffset now, CancellationToken cancellationToken);
    Task FailDeliveryAsync(Guid reservationId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OtpVerificationResult> VerifyAsync(Guid challengeId, string code, int maximumWrongAttempts,
        DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IOtpDelivery
{
    Task SendAsync(Guid challengeId, string mobileE164, string code, DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public interface IOtpSender
{
    Task SendAsync(Guid challengeId, string mobileE164, string code, DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

/// <summary>Available only through a Development/test service provider, never an HTTP route.</summary>
public interface ILocalOtpInspection
{
    bool TryGetCode(Guid challengeId, out string code);
}

public sealed class OtpRateLimitException : Exception
{
    public OtpRateLimitException() : base("The verification request cannot be completed right now.") { }
}

public sealed class OtpDeliveryUnavailableException : Exception
{
    public OtpDeliveryUnavailableException() : base("Verification delivery is unavailable.") { }
}
