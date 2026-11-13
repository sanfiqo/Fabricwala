namespace TextileConnect.Domain.Community;
public sealed class LogisticsAssignment { public long Id { get; set; } public long SellerOrderId { get; set; } public long LogisticsBusinessId { get; set; } public string Status { get; set; } = "Assigned"; }
