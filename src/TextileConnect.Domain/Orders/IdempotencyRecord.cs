namespace TextileConnect.Domain.Orders;

/// <summary>Remembers the result of a mutating request so a retry with the same Idempotency-Key returns it again.</summary>
public sealed class IdempotencyRecord
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public required string Scope { get; set; }
    public required string Key { get; set; }
    public required string RequestHash { get; set; }
    /// <summary>Public key of the created resource (order number or gateway order id).</summary>
    public required string ResourceKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
