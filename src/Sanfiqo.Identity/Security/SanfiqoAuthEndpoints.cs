using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
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
            IMobileAuthentication authentication, UserManager<ApplicationUser> users,
            IUserClaimsPrincipalFactory<ApplicationUser> principals, CancellationToken cancellationToken) =>
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
            var principal = await principals.CreateAsync(user);
            var roles = await users.GetRolesAsync(user);
            var business = httpContext.RequestServices.GetService<IAuthBusinessProfileReader>() is { } reader
                ? await reader.ReadAsync(user.Id, cancellationToken) : null;
            var claimsIdentity = (ClaimsIdentity)principal.Identity!;
            foreach (var claim in claimsIdentity.Claims.Where(x => BusinessClaimTypes.All.Contains(x.Type)).ToArray()) claimsIdentity.RemoveClaim(claim);
            if (business is not null)
            {
                var identity = (ClaimsIdentity)principal.Identity!;
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessId, business.Id.ToString()));
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessType, business.Type));
                if (business.VerificationStatus == "Approved")
                    identity.AddClaim(new Claim(BusinessClaimTypes.Approved, "true"));
            }
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            httpContext.Response.Cookies.Delete("tc_otp_challenge", new CookieOptions { Path = "/api/v1/auth" });
            return Results.Ok(new AuthProfileResponse(user.Id, user.PhoneNumber, user.FullName, roles, business, user.PreferredLanguage));
        }).RequireRateLimiting("auth");

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, IAuthBusinessProfileReader? reader) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId is null ? null : await users.FindByIdAsync(userId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            var business = reader is null ? null : await reader.ReadAsync(user.Id);
            return Results.Ok(new AuthProfileResponse(user.Id, user.PhoneNumber, user.FullName, roles, business, user.PreferredLanguage));
        })
            .RequireAuthorization();
        group.MapPost("/refresh", async (HttpContext context, UserManager<ApplicationUser> users,
            IUserClaimsPrincipalFactory<ApplicationUser> principals, IAuthBusinessProfileReader? reader, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var principal = await principals.CreateAsync(user);
            var business = reader is null ? null : await reader.ReadAsync(user.Id, ct);
            var identity = (ClaimsIdentity)principal.Identity!;
            foreach (var claim in identity.Claims.Where(x => BusinessClaimTypes.All.Contains(x.Type)).ToArray()) identity.RemoveClaim(claim);
            if (business is not null)
            {
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessId, business.Id.ToString()));
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessType, business.Type));
                if (business.VerificationStatus == "Approved") identity.AddClaim(new Claim(BusinessClaimTypes.Approved, "true"));
            }
            await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            return Results.Ok(new AuthProfileResponse(user.Id, user.PhoneNumber, user.FullName,
                await users.GetRolesAsync(user), business, user.PreferredLanguage));
        }).RequireAuthorization();
        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
        return endpoints;
    }

    public sealed record OtpRequest(string PhoneNumber);
    public sealed record OtpVerifyRequest(string PhoneNumber, string Code, Guid? ChallengeId = null);
}
