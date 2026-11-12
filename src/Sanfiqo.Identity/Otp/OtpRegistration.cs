using Sanfiqo.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sanfiqo.Identity;

public static class OtpRegistration
{
    public static IServiceCollection AddOtpChallengeFoundation(this IServiceCollection services,
        IConfiguration configuration, string environmentName)
    {
        var policy = configuration.GetSection("Otp:Policy").Get<OtpPolicy>() ?? new OtpPolicy();
        policy.StorageHmacKey = configuration["Otp:StorageHmacKey"] ?? policy.StorageHmacKey;
        var policyErrors = policy.Validate();
        if (policyErrors.Count != 0)
            throw new InvalidOperationException("User OTP policy configuration is invalid: " + string.Join(" ", policyErrors));

        services.AddSingleton(policy);
        services.AddScoped<IOtpChallenges, OtpChallengeService>();
        services.AddScoped<IOtpChallengeStore, EfOtpChallengeStore>();
        services.AddDataProtection();

        var localEnvironment = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase);
        var provider = configuration["Otp:Delivery:Provider"];
        if (string.IsNullOrWhiteSpace(provider)) provider = localEnvironment ? "Fake" : "Unconfigured";
        if (provider.Equals("Fake", StringComparison.OrdinalIgnoreCase))
        {
            if (!localEnvironment)
                throw new InvalidOperationException("The fake user OTP delivery adapter is restricted to Development/test.");
            services.AddSingleton<FakeOtpDelivery>();
            services.AddSingleton<IOtpDelivery>(sp => sp.GetRequiredService<FakeOtpDelivery>());
            services.AddSingleton<ILocalOtpInspection>(sp => sp.GetRequiredService<FakeOtpDelivery>());
        }
        else if (provider.Equals("Unconfigured", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IOtpDelivery, UnconfiguredOtpDelivery>();
        }
        else
        {
            throw new InvalidOperationException("The configured user OTP delivery provider is not supported.");
        }

        return services;
    }
}
