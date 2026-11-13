namespace TextileConnect.Domain.Community;
public sealed class Review { public long Id { get; set; } public long ProductId { get; set; } public long RetailerBusinessId { get; set; } public int Rating { get; set; } public string? Comment { get; set; } }
