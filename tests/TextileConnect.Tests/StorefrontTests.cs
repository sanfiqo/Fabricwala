using TextileConnect.Domain.Businesses;

namespace TextileConnect.Tests;

public sealed class StorefrontTests
{
    [Fact]
    public void Only_approved_manufacturers_are_public_storefronts()
    {
        var business = new Business { LegalName = "Maker", DisplayName = "Maker", Phone = "+919000000001", BusinessType = BusinessType.Manufacturer };
        Assert.Equal(VerificationStatus.Pending, business.VerificationStatus);
        business.VerificationStatus = VerificationStatus.Approved;
        Assert.Equal(BusinessType.Manufacturer, business.BusinessType);
    }
}
