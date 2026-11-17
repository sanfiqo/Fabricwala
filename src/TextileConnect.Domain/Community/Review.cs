namespace TextileConnect.Domain.Community;
public sealed class Review : ITimestamped { public long Id { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow; public long ProductId { get; set; } public long RetailerBusinessId { get; set; } public int Rating { get; set; } public string? Comment { get; set; } }
