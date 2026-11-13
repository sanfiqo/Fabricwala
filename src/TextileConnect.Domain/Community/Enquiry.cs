namespace TextileConnect.Domain.Community;
public sealed class Enquiry { public long Id { get; set; } public long RetailerBusinessId { get; set; } public long? ProductId { get; set; } public required string Description { get; set; } public string Status { get; set; } = "Open"; }
