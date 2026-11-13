namespace TextileConnect.Domain.Community;
public sealed class GroupBuy { public long Id { get; set; } public long ProductId { get; set; } public long OwnerBusinessId { get; set; } public int TargetQuantity { get; set; } public DateTime ClosesAtUtc { get; set; } public string Status { get; set; } = "Open"; }
