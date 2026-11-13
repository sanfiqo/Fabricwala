using TextileConnect.Domain.Orders;

namespace TextileConnect.Tests;

public sealed class ReturnClaimTests
{
    [Fact]
    public void Claim_starts_submitted_and_can_track_partial_refund()
    {
        var claim = new ReturnClaim { SellerOrderId = 7, Reason = "Damaged fabric" };
        Assert.Equal("Submitted", claim.Status);
        claim.Status = "PartiallyRefunded"; claim.ApprovedRefundAmount = 125;
        Assert.Equal(125, claim.ApprovedRefundAmount);
    }
}
