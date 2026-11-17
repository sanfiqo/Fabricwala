namespace TextileConnect.Domain.Payments;

/// <summary>One delivery of a gateway webhook, kept so that the same event is never applied twice. Unique per gateway and event id.</summary>
public sealed class PaymentWebhookEvent
{
    public long Id { get; set; }
    public required string Gateway { get; set; }
    public required string EventId { get; set; }
    public required string EventType { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Null while the event is being handled or when handling failed; the gateway sends it again later.</summary>
    public DateTime? ProcessedAtUtc { get; set; }
}
