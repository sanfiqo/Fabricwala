using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Sanfiqo.Identity;

public static class SanfiqoIdentityRegistration
{
    public static IServiceCollection AddSanfiqoIdentity(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<SanfiqoIdentityOptions>()
            .Bind(configuration.GetSection(SanfiqoIdentityOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Identity cookie name is required.")
            .ValidateOnStart();
        services.AddDataProtection().PersistKeysToDbContext<ApplicationIdentityDbContext>();
        services.AddApplicationIdentityPersistence();
        services.AddOtpChallengeFoundation(configuration, environment.EnvironmentName);
        services.AddUserContactOwnership();
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, SanfiqoClaimsPrincipalFactory>();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        }).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            var identityOptions = configuration.GetSection(SanfiqoIdentityOptions.SectionName).Get<SanfiqoIdentityOptions>() ?? new();
            options.Cookie.Name = identityOptions.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = identityOptions.CookieLifetime;
            options.SlidingExpiration = identityOptions.CookieSlidingExpiration;
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
                {
                    context.RejectPrincipal();
                    return;
                }

                // While the admin second step is required, an Admin session that never passed it is not accepted (a session from before the switch is asked to sign in again).
                var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                if (AdminMfa.IsRequired(configuration) && context.Principal!.IsInRole(SanfiqoRoleNames.Admin) && !AdminMfa.HasPassed(context.Principal))
                {
                    context.RejectPrincipal();
                    return;
                }

                var currentStamp = await userManager.GetSecurityStampAsync(user);
                var cookieStamp = context.Principal?.FindFirstValue(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType);
                if (SecurityStampRefreshPolicy.NeedsRenewal(cookieStamp, currentStamp))
                {
                    var factory = context.HttpContext.RequestServices.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
                    var renewed = await factory.CreateAsync(user);
                    AdminMfa.Apply(renewed, user, AdminMfa.IsRequired(configuration), AdminMfa.HasPassed(context.Principal!));
                    context.ReplacePrincipal(renewed);
                    context.ShouldRenew = true;
                }
            };
        });
        services.AddAuthorization();
        services.AddAntiforgery(options => options.HeaderName =
            configuration[$"{SanfiqoIdentityOptions.SectionName}:AntiforgeryHeaderName"] ?? "X-CSRF-TOKEN");
        return services;
    }
}
