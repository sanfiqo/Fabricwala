using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace Sanfiqo.Identity;

public static class SanfiqoAuthEndpoints
{
    public static IEndpointRouteBuilder MapSanfiqoAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/otp/request", async (OtpRequest request, HttpContext httpContext,
            IMobileAuthentication authentication, IHostEnvironment environment, CancellationToken cancellationToken) =>
        {
            if (!UserContactNormalizer.TryNormalize(UserContactKind.Mobile, request.PhoneNumber, out _)) return Results.BadRequest();
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ip)) return Results.BadRequest();
            try
            {
                var challengeId = await authentication.RequestMobileCodeAsync(request.PhoneNumber, ip, cancellationToken);
                httpContext.Response.Cookies.Append("tc_otp_challenge", challengeId.ToString(), new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment() || httpContext.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/api/v1/auth", MaxAge = TimeSpan.FromMinutes(5) });
                return Results.NoContent();
            }
            catch (OtpRateLimitException limit)
            {
                httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(limit.RetryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }
            catch (OtpDeliveryUnavailableException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        }).RequireRateLimiting("auth");

        group.MapPost("/otp/verify", async (OtpVerifyRequest request, HttpContext httpContext,
            IMobileAuthentication authentication, UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
        {
            if (!UserContactNormalizer.TryNormalize(UserContactKind.Mobile, request.PhoneNumber, out _)
                || request.Code is null || request.Code.Length != 6 || request.Code.Any(c => c is < '0' or > '9'))
                return Results.BadRequest();
            var challengeId = request.ChallengeId ?? (Guid.TryParse(httpContext.Request.Cookies["tc_otp_challenge"], out var cookieId) ? cookieId : Guid.Empty);
            var result = await authentication.CompleteMobileSignInAsync(challengeId, request.PhoneNumber,
                request.Code, cancellationToken);
            if (result.Status == MobileAuthenticationStatus.Unavailable) return Results.StatusCode(503);
            if (result.Status != MobileAuthenticationStatus.Authenticated || result.UserId is null)
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(result.UserId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            // A new sign-in has not passed the admin second step yet, whatever the old cookie said.
            var principal = await CreateSessionAsync(httpContext, user, passed: false, cancellationToken);
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            httpContext.Response.Cookies.Delete("tc_otp_challenge", new CookieOptions { Path = "/api/v1/auth" });
            return Results.Ok(await ProfileAsync(httpContext, users, user, principal, cancellationToken));
        }).RequireRateLimiting("auth");

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapGet("/me", async (HttpContext httpContext, ClaimsPrincipal principal, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId is null ? null : await users.FindByIdAsync(userId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            return Results.Ok(await ProfileAsync(httpContext, users, user, principal, ct));
        })
            .RequireAuthorization();
        group.MapPost("/refresh", async (HttpContext context, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            // A refresh keeps the admin second step as it is: it never grants it.
            var principal = await CreateSessionAsync(context, user, AdminMfa.HasPassed(context.User), ct);
            await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            return Results.Ok(await ProfileAsync(context, users, user, principal, ct));
        }).RequireAuthorization();
        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        MapMfa(group);
        return endpoints;
    }

    // ---- the admin second step ----------------------------------------------------------------------------------------------------

    private static void MapMfa(RouteGroupBuilder auth)
    {
        var mfa = auth.MapGroup("/mfa").RequireAuthorization().RequireRateLimiting("auth");

        // Starts (or restarts, while it is not switched on) the enrolment: a new secret for the authenticator app.
        mfa.MapPost("/setup", async (HttpContext httpContext, ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await EnrollingUserAsync(principal, users);
            if (user is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (user.TwoFactorEnabled) return Results.Conflict(new { code = "mfa_already_enabled", message = "The second step is already switched on." });
            await users.ResetAuthenticatorKeyAsync(user);
            var key = (await users.GetAuthenticatorKeyAsync(user))!;
            var label = Uri.EscapeDataString(user.PhoneNumber ?? user.UserName ?? user.Id);
            return Results.Ok(new MfaSetupResponse(key, $"otpauth://totp/Fabricwala:{label}?secret={key}&issuer=Fabricwala&digits=6"));
        });

        // Finishes the enrolment with a code from the app, switches the second step on, and returns the one-time recovery codes (shown once).
        mfa.MapPost("/enable", async (MfaCodeRequest request, HttpContext httpContext, ClaimsPrincipal principal, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var user = await EnrollingUserAsync(principal, users);
            if (user is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (user.TwoFactorEnabled) return Results.Conflict(new { code = "mfa_already_enabled", message = "The second step is already switched on." });
            if (await users.GetAuthenticatorKeyAsync(user) is null) return Results.BadRequest(new { code = "mfa_not_started", message = "Start the setup first." });
            if (await users.IsLockedOutAsync(user)) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            if (!await CodeIsRightAsync(users, user, request.Code)) return await RefuseAsync(users, user);
            await users.ResetAccessFailedCountAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            var recovery = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? [];
            var session = await CreateSessionAsync(httpContext, user, passed: true, ct);
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, session);
            return Results.Ok(new MfaEnabledResponse(recovery.ToList(), await ProfileAsync(httpContext, users, user, session, ct)));
        });

        // The second step of a sign-in: a code from the app, or one of the recovery codes (each works once).
        mfa.MapPost("/verify", async (MfaCodeRequest request, HttpContext httpContext, ClaimsPrincipal principal, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (AdminMfa.PendingKind(principal) != AdminMfa.Verify) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive || !user.TwoFactorEnabled) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (await users.IsLockedOutAsync(user)) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            var code = Normalise(request.Code);
            // Six digits is an app code; anything else is tried as a recovery code exactly as written (they contain a hyphen).
            var accepted = code.Length == 6 && code.All(char.IsAsciiDigit)
                ? await CodeIsRightAsync(users, user, code)
                : !string.IsNullOrWhiteSpace(request.Code) && (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.Code.Trim())).Succeeded;
            if (!accepted) return await RefuseAsync(users, user);
            await users.ResetAccessFailedCountAsync(user);
            var session = await CreateSessionAsync(httpContext, user, passed: true, ct);
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, session);
            return Results.Ok(await ProfileAsync(httpContext, users, user, session, ct));
        });
    }

    /// <summary>The user who may enrol now: an Admin session that is waiting to enrol, or a signed-in Admin who switches it on by choice. Null for everyone else.</summary>
    private static async Task<ApplicationUser?> EnrollingUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var allowed = AdminMfa.PendingKind(principal) == AdminMfa.Enrol || principal.IsInRole(SanfiqoRoleNames.Admin);
        if (!allowed) return null;
        var user = await users.GetUserAsync(principal);
        return user is { IsActive: true } ? user : null;
    }

    private static Task<bool> CodeIsRightAsync(UserManager<ApplicationUser> users, ApplicationUser user, string? code)
        => users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, Normalise(code));

    private static string Normalise(string? code) => (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

    /// <summary>A wrong code counts toward the lockout of the user (5 tries, then a few minutes), on top of the rate limit per address.</summary>
    private static async Task<IResult> RefuseAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        if (!user.LockoutEnabled) await users.SetLockoutEnabledAsync(user, true);
        await users.AccessFailedAsync(user);
        return Results.Unauthorized();
    }

    // ---- sessions -------------------------------------------------------------------------------------------------------------------

    /// <summary>The principal of a session: the claims of the user, the business claims, and the admin second step applied (see <see cref="AdminMfa"/>).</summary>
    private static async Task<ClaimsPrincipal> CreateSessionAsync(HttpContext httpContext, ApplicationUser user, bool passed, CancellationToken cancellationToken)
    {
        var services = httpContext.RequestServices;
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        var business = services.GetService<IAuthBusinessProfileReader>() is { } reader ? await reader.ReadAsync(user.Id, cancellationToken) : null;
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.Claims.Where(x => BusinessClaimTypes.All.Contains(x.Type)).ToArray()) identity.RemoveClaim(claim);
        if (business is not null)
        {
            identity.AddClaim(new Claim(BusinessClaimTypes.BusinessId, business.Id.ToString()));
            identity.AddClaim(new Claim(BusinessClaimTypes.BusinessType, business.Type));
            if (business.VerificationStatus == "Approved") identity.AddClaim(new Claim(BusinessClaimTypes.Approved, "true"));
        }
        AdminMfa.Apply(principal, user, AdminMfa.IsRequired(services.GetRequiredService<IConfiguration>()), passed);
        return principal;
    }

    /// <summary>The profile the screens show. A session that is still waiting for the second step shows no roles and says what is pending.</summary>
    private static async Task<AuthProfileResponse> ProfileAsync(HttpContext httpContext, UserManager<ApplicationUser> users, ApplicationUser user, ClaimsPrincipal session, CancellationToken cancellationToken)
    {
        var pending = AdminMfa.PendingKind(session);
        var reader = httpContext.RequestServices.GetService<IAuthBusinessProfileReader>();
        var business = reader is null ? null : await reader.ReadAsync(user.Id, cancellationToken);
        IList<string> roles = pending is null ? await users.GetRolesAsync(user) : [];
        return new AuthProfileResponse(user.Id, user.PhoneNumber, user.FullName, roles, business, user.PreferredLanguage, pending);
    }

    public sealed record OtpRequest(string PhoneNumber);
    public sealed record OtpVerifyRequest(string PhoneNumber, string Code, Guid? ChallengeId = null);
    public sealed record MfaCodeRequest(string Code);
    public sealed record MfaSetupResponse(string SharedKey, string AuthenticatorUri);
    public sealed record MfaEnabledResponse(IReadOnlyList<string> RecoveryCodes, AuthProfileResponse Profile);
}
