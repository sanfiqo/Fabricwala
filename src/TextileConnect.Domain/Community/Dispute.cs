namespace TextileConnect.Domain.Community;
public sealed class Dispute { public long Id { get; set; } public long SellerOrderId { get; set; } public long RaisedByBusinessId { get; set; } public required string Reason { get; set; } public string Status { get; set; } = "Open"; }
