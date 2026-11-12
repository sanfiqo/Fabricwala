namespace Sanfiqo.Identity;

public sealed class OtpChallenge
{
    public Guid Id { get; set; }
    public required byte[] MobileHash { get; set; }
    public required byte[] CodeHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int WrongAttempts { get; set; }
    public byte State { get; set; } // 0 pending, 1 active, 2 invalidated/expired, 3 exhausted, 4 delivery failed, 5 successfully verified
}

public sealed class OtpSendEvent
{
    public long Id { get; set; }
    public Guid ChallengeId { get; set; }
    public required byte[] MobileHash { get; set; }
    public required byte[] IpHash { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>A migration-seeded row used to serialize OTP limit/consume transitions on MySQL.</summary>
public sealed class OtpWriteGate
{
    public int Id { get; set; }
}
