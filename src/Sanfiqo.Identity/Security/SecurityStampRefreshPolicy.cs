namespace Sanfiqo.Identity;

public static class SecurityStampRefreshPolicy
{
    public static bool NeedsRenewal(string? cookieStamp, string? currentStamp)
        => !string.Equals(cookieStamp, currentStamp, StringComparison.Ordinal);
}
