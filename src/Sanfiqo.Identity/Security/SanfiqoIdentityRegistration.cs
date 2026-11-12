using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sanfiqo.Identity;

public static class SanfiqoIdentityRegistration
{
    public static IServiceCollection AddSanfiqoIdentity(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddApplicationIdentityPersistence();
        services.AddOtpChallengeFoundation(configuration, environment.EnvironmentName);
        services.AddUserContactOwnership();
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, SanfiqoClaimsPrincipalFactory>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        }).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = ".AspNetCore.TextileConnect.Identity";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.GetUserAsync(context.Principal!);
                if (user is null || !user.IsActive)
                    context.RejectPrincipal();
            };
        });
        services.AddAuthorization();
        services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        return services;
    }
}
