using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Sanfiqo.Identity;

public static class SanfiqoAuthEndpoints
{
    public static IEndpointRouteBuilder MapSanfiqoAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/otp/request", async (OtpRequest request, HttpContext httpContext,
            IMobileAuthentication authentication, CancellationToken cancellationToken) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ip)) return Results.BadRequest();
            try
            {
                var challengeId = await authentication.RequestMobileCodeAsync(request.PhoneNumber, ip, cancellationToken);
                httpContext.Response.Cookies.Append("tc_otp_challenge", challengeId.ToString(), new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(5) });
                return Results.NoContent();
            }
            catch (OtpRateLimitException) { return Results.StatusCode(StatusCodes.Status429TooManyRequests); }
            catch (OtpDeliveryUnavailableException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        });

        group.MapPost("/otp/verify", async (OtpVerifyRequest request, HttpContext httpContext,
            IMobileAuthentication authentication, UserManager<ApplicationUser> users,
            IUserClaimsPrincipalFactory<ApplicationUser> principals, CancellationToken cancellationToken) =>
        {
            var challengeId = request.ChallengeId ?? (Guid.TryParse(httpContext.Request.Cookies["tc_otp_challenge"], out var cookieId) ? cookieId : Guid.Empty);
            var result = await authentication.CompleteMobileSignInAsync(challengeId, request.PhoneNumber,
                request.Code, cancellationToken);
            if (result.Status != MobileAuthenticationStatus.Authenticated || result.UserId is null)
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(result.UserId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var principal = await principals.CreateAsync(user);
            var roles = await users.GetRolesAsync(user);
            var business = httpContext.RequestServices.GetService<IAuthBusinessProfileReader>() is { } reader
                ? await reader.ReadAsync(user.Id, cancellationToken) : null;
            if (business is not null)
            {
                var identity = (ClaimsIdentity)principal.Identity!;
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessId, business.Id.ToString()));
                identity.AddClaim(new Claim(BusinessClaimTypes.BusinessType, business.Type));
                if (business.VerificationStatus == "Approved")
                    identity.AddClaim(new Claim(BusinessClaimTypes.Approved, "true"));
            }
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            httpContext.Response.Cookies.Delete("tc_otp_challenge");
            return Results.Ok(new { userId = user.Id, phone = user.PhoneNumber, fullName = user.UserName, roles, business });
        });

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, IAuthBusinessProfileReader? reader) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId is null ? null : await users.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            var business = reader is null ? null : await reader.ReadAsync(user.Id);
            return Results.Ok(new { userId = user.Id, phone = user.PhoneNumber, fullName = user.UserName, roles, business });
        })
            .RequireAuthorization();
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
