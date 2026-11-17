namespace TextileConnect.Domain.Community;
public sealed class LogisticsAssignment : ITimestamped { public long Id { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow; public long SellerOrderId { get; set; } public long LogisticsBusinessId { get; set; } public string Status { get; set; } = "Assigned"; }
