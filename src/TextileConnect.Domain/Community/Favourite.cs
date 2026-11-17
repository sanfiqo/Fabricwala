namespace TextileConnect.Domain.Community;
public sealed class Favourite : ITimestamped { public long Id { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow; public long RetailerBusinessId { get; set; } public long ProductId { get; set; } }
