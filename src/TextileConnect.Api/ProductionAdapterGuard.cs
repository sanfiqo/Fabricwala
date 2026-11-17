using Sanfiqo.Identity;
using TextileConnect.Application.Files;
using TextileConnect.Application.Notifications;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Files;
using TextileConnect.Infrastructure.Payments;

namespace TextileConnect.Api;

/// <summary>
/// Fails the start of the app outside Development and Testing when a development or unconfigured adapter is still wired in,
/// so a wrong deployment stops at startup instead of failing at the first request. Every problem is listed, with the config key to set.
/// </summary>
public static class ProductionAdapterGuard
{
    public static void Validate(IServiceProvider services, IConfiguration configuration, string environmentName)
    {
        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)) return;

        var problems = Problems(services, configuration);
        if (problems.Count != 0)
            throw new InvalidOperationException("The app cannot start in " + environmentName + " with development adapters:" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Select(x => " - " + x)));
    }

    public static IReadOnlyList<string> Problems(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var problems = new List<string>();

        var storage = provider.GetService<IFileStorage>();
        if (storage is null || storage is DevelopmentFileStorage)
            problems.Add("File storage: no production IFileStorage is registered. Configure Storage:R2:AccountId, AccessKeyId, SecretAccessKey and BucketName (adapter not built yet, task F3.1).");

        var gateway = provider.GetService<IPaymentGateway>();
        if (gateway is null || gateway is DevelopmentPaymentGateway)
            problems.Add("Payments: the development payment gateway is registered. Register the Razorpay gateway.");
        else if (gateway is RazorpayPaymentGateway)
            foreach (var name in new[] { "KeyId", "KeySecret", "WebhookSecret" })
                if (string.IsNullOrWhiteSpace(RazorpayPaymentGateway.Find(configuration, name)))
                    problems.Add($"Payments: Payments:Razorpay:{name} is not set.");

        var otp = provider.GetService<IOtpDelivery>();
        if (otp is null or UnconfiguredOtpDelivery or FakeOtpDelivery)
            problems.Add("OTP: no SMS sender is registered, so nobody can sign in. Configure Otp:Msg91:AuthKey and Otp:Msg91:TemplateId (adapter not built yet, task F3.2).");

        // INotificationSender is not wired into any flow yet (task F3.3). It is only checked if somebody registers it.
        if (provider.GetService<INotificationSender>() is DevelopmentNotificationSender)
            problems.Add("Notifications: the development notification sender is registered.");

        return problems;
    }
}
