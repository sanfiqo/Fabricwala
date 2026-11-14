using TextileConnect.Domain.Orders;

namespace TextileConnect.Api;

/// <summary>PaymentOnHold is true while the claim is unresolved: the seller payout for this order is not released.</summary>
public sealed record ClaimResponse(long Id, long SellerOrderId, string Reason, int Pieces, string Note, string Status, string? SellerResponse, string? AdminDecision,
    decimal? ApprovedRefundAmount, int PhotoCount, bool PaymentOnHold, DateTime CreatedAt, DateTime? ResolvedAt)
{
    public static ClaimResponse From(ReturnClaim x) => new(x.Id, x.SellerOrderId, x.ReasonCode.ToString(), x.Pieces, x.Reason, x.Status, x.SellerResponse, x.AdminDecision,
        x.ApprovedRefundAmount, x.Photos.Count, x.ResolvedAtUtc is null, x.CreatedAtUtc, x.ResolvedAtUtc);
}
