using Sanfiqo.Identity;

namespace TextileConnect.Tests;

public sealed class SecurityStampRefreshTests
{
    [Fact]
    public void Changed_security_stamp_requires_cookie_renewal()
        => Assert.True(SecurityStampRefreshPolicy.NeedsRenewal("old-stamp", "new-stamp"));

    [Fact]
    public void Matching_security_stamp_keeps_cookie()
        => Assert.False(SecurityStampRefreshPolicy.NeedsRenewal("same-stamp", "same-stamp"));
}
