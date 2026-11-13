using TextileConnect.Domain.Businesses;

namespace TextileConnect.Tests;

public sealed class BusinessSuspensionTests
{
    [Fact]
    public void Suspension_status_is_distinct_from_pending_and_approved()
    {
        Assert.NotEqual(VerificationStatus.Pending, VerificationStatus.Suspended);
        Assert.NotEqual(VerificationStatus.Approved, VerificationStatus.Suspended);
    }
}
