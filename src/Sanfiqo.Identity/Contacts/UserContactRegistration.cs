using Sanfiqo.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Sanfiqo.Identity;

public static class UserContactRegistration
{
    public static IServiceCollection AddUserContactOwnership(this IServiceCollection services)
    {
        services.AddSingleton<UserContactCryptography>();
        services.AddScoped<IUserContactProofProtector>(provider =>
            provider.GetRequiredService<UserContactCryptography>());
        services.AddScoped<IUserContactVerificationProofIssuer, UserContactVerificationProofIssuer>();
        services.AddScoped<IUserContactVerificationEvidence, OtpUserContactVerificationEvidence>();
        services.AddScoped<IUserContactOwnershipStore, EfUserContactOwnershipStore>();
        services.AddScoped<IUserContactOwnership, UserContactOwnershipService>();
        services.AddScoped<IVerifiedMobileAccountStore, EfVerifiedMobileAccountStore>();
        services.AddScoped<IMobileAuthentication, MobileAuthenticationService>();
        return services;
    }
}
