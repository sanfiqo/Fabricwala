namespace TextileConnect.Domain.Payments;

/// <summary>
/// One thing that was done: who (UserId, or "system" for background jobs and "anonymous" for callers without a sign-in such as the payment gateway),
/// what (Action, EntityName, EntityId), when (CreatedAtUtc) and from where (IpAddress).
/// Details is a short plain-text note (an amount, a status). It never holds a request body, an OTP, a document key or a secret.
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
