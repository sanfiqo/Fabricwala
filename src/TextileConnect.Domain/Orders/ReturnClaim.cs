namespace TextileConnect.Domain.Orders;

public sealed class ReturnClaim
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public SellerOrder? SellerOrder { get; set; }
    public required string Reason { get; set; }
    public string Status { get; set; } = "Submitted";
    public string? SellerResponse { get; set; }
    public string? AdminDecision { get; set; }
    public decimal? ApprovedRefundAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public ICollection<ReturnClaimPhoto> Photos { get; set; } = new List<ReturnClaimPhoto>();
}

public sealed class ReturnClaimPhoto
{
    public long Id { get; set; }
    public long ReturnClaimId { get; set; }
    public required string FileKey { get; set; }
}
