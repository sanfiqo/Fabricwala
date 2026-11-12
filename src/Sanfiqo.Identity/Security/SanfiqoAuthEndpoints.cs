using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
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
                var challengeId = await authentication.RequestMobileCodeAsync(request.Mobile, ip, cancellationToken);
                return Results.Ok(new { challengeId });
            }
            catch (OtpRateLimitException) { return Results.StatusCode(StatusCodes.Status429TooManyRequests); }
            catch (OtpDeliveryUnavailableException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        });

        group.MapPost("/otp/verify", async (OtpVerifyRequest request, HttpContext httpContext,
            IMobileAuthentication authentication, UserManager<ApplicationUser> users,
            IUserClaimsPrincipalFactory<ApplicationUser> principals, CancellationToken cancellationToken) =>
        {
            var result = await authentication.CompleteMobileSignInAsync(request.ChallengeId, request.Mobile,
                request.Code, cancellationToken);
            if (result.Status != MobileAuthenticationStatus.Authenticated || result.UserId is null)
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(result.UserId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var principal = await principals.CreateAsync(user);
            await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            return Results.Ok(new { userId = user.Id });
        });

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(new { userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) }))
            .RequireAuthorization();
        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
        return endpoints;
    }

    public sealed record OtpRequest(string Mobile);
    public sealed record OtpVerifyRequest(Guid ChallengeId, string Mobile, string Code);
}
