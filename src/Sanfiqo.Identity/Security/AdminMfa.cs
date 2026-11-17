using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace Sanfiqo.Identity;

/// <summary>
/// Second step of the admin sign-in: a six digit code from an authenticator app (TOTP) after the mobile one-time code.
/// Switched by the setting <c>Auth:AdminMfa</c>: "Off" (the default, so nobody is locked out before an admin has enrolled) or "Required".
/// While it is required an Admin session starts <b>without the Admin role</b> and carries a <c>mfa_pending</c> claim ("enrol" when the admin has no authenticator yet,
/// "verify" when they have): every admin route then answers 403 until the code is accepted, which signs in again with the role and an <c>amr=mfa</c> claim.
/// Every place that builds a session from the database (sign-in, refresh, the cookie revalidation after a security stamp change) goes through <see cref="Apply"/>,
/// so none of them can hand the role back without the second step.
/// </summary>
public static class AdminMfa
{
    public const string ConfigKey = "Auth:AdminMfa";
    public const string PendingClaim = "mfa_pending";
    public const string AuthMethodClaim = "amr";
    public const string AuthMethodMfa = "mfa";
    public const string Enrol = "enrol";
    public const string Verify = "verify";

    public static bool IsRequired(IConfiguration configuration) => string.Equals(configuration[ConfigKey], "Required", StringComparison.OrdinalIgnoreCase);

    public static bool HasPassed(ClaimsPrincipal principal) => principal.HasClaim(AuthMethodClaim, AuthMethodMfa);

    /// <summary>"enrol", "verify" or null when the session is not waiting for the second step.</summary>
    public static string? PendingKind(ClaimsPrincipal principal) => principal.FindFirstValue(PendingClaim);

    /// <summary>
    /// Shapes a principal that was just made from the database. When the second step is required and the user is an Admin: with <paramref name="passed"/> the principal gets the
    /// <c>amr=mfa</c> claim, without it every role is taken away and the pending claim says what the user has to do.
    /// </summary>
    public static void Apply(ClaimsPrincipal principal, ApplicationUser user, bool required, bool passed)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.Claims.Where(x => x.Type is PendingClaim or AuthMethodClaim).ToArray()) identity.RemoveClaim(claim);
        if (!required || !principal.IsInRole(SanfiqoRoleNames.Admin)) return;
        if (passed) { identity.AddClaim(new Claim(AuthMethodClaim, AuthMethodMfa)); return; }
        foreach (var role in identity.Claims.Where(x => x.Type == identity.RoleClaimType).ToArray()) identity.RemoveClaim(role);
        identity.AddClaim(new Claim(PendingClaim, user.TwoFactorEnabled ? Verify : Enrol));
    }
}
