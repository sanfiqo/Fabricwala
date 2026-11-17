namespace TextileConnect.Domain.Community;
public sealed class GroupBuyParticipant : ITimestamped { public long Id { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow; public long GroupBuyId { get; set; } public long BusinessId { get; set; } public int Quantity { get; set; } }
