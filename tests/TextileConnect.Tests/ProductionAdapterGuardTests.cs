using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sanfiqo.Identity;
using TextileConnect.Api;
using TextileConnect.Application.Files;
using TextileConnect.Application.Notifications;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Files;
using TextileConnect.Infrastructure.Payments;

namespace TextileConnect.Tests;

public sealed class ProductionAdapterGuardTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    private static ServiceProvider Services(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider();
    }

    private static readonly (string, string)[] RazorpayKeys = [("Payments:Razorpay:KeyId", "k"), ("Payments:Razorpay:KeySecret", "s"), ("Payments:Razorpay:WebhookSecret", "w")];

    [Fact]
    public void Development_adapters_are_reported_with_the_config_key_to_set()
    {
        using var provider = Services(s =>
        {
            s.AddSingleton<IFileStorage>(new DevelopmentFileStorage(new FakeHostEnvironment()));
            s.AddSingleton<IPaymentGateway, DevelopmentPaymentGateway>();
            s.AddSingleton<IOtpDelivery, UnconfiguredOtpDelivery>();
            s.AddSingleton<INotificationSender, DevelopmentNotificationSender>();
        });

        var problems = ProductionAdapterGuard.Problems(provider, Config());

        Assert.Equal(4, problems.Count);
        Assert.Contains(problems, x => x.Contains("Storage:R2:"));
        Assert.Contains(problems, x => x.Contains("payment gateway"));
        Assert.Contains(problems, x => x.Contains("Otp:Msg91:"));
        Assert.Contains(problems, x => x.Contains("notification sender"));
    }

    [Fact]
    public void Nothing_registered_is_a_problem_but_an_unregistered_notification_sender_is_not()
    {
        using var provider = Services(_ => { });
        var problems = ProductionAdapterGuard.Problems(provider, Config());
        Assert.Equal(3, problems.Count);
        Assert.DoesNotContain(problems, x => x.Contains("notification"));
    }

    [Fact]
    public void Razorpay_with_missing_keys_names_each_missing_key_and_accepts_the_old_names()
    {
        void Register(IServiceCollection s)
        {
            s.AddSingleton<IFileStorage, StubStorage>();
            s.AddSingleton<IOtpDelivery, StubOtp>();
            s.AddSingleton<IPaymentGateway>(sp => new RazorpayPaymentGateway(new HttpClient(), sp.GetRequiredService<IConfiguration>()));
        }

        using var missing = Services(s => { s.AddSingleton(Config(("Payments:Razorpay:KeyId", "k"))); Register(s); });
        var problems = ProductionAdapterGuard.Problems(missing, Config(("Payments:Razorpay:KeyId", "k")));
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, x => x.Contains("Payments:Razorpay:KeySecret"));
        Assert.Contains(problems, x => x.Contains("Payments:Razorpay:WebhookSecret"));

        var complete = Config(RazorpayKeys);
        using var ok = Services(s => { s.AddSingleton(complete); Register(s); });
        Assert.Empty(ProductionAdapterGuard.Problems(ok, complete));

        var legacy = Config(("Razorpay:KeyId", "k"), ("Razorpay:KeySecret", "s"), ("Razorpay:WebhookSecret", "w"));
        using var old = Services(s => { s.AddSingleton(legacy); Register(s); });
        Assert.Empty(ProductionAdapterGuard.Problems(old, legacy));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void Validate_throws_only_outside_development_and_testing(string environment, bool throws)
    {
        using var provider = Services(_ => { });
        var act = () => ProductionAdapterGuard.Validate(provider, Config(), environment);
        if (!throws) { act(); return; }
        var error = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Storage:R2:", error.Message);
        Assert.Contains("Otp:Msg91:", error.Message);
    }

    private sealed class StubStorage : IFileStorage
    {
        public Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default) => Task.FromResult("key");
        public Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class StubOtp : IOtpDelivery
    {
        public Task SendAsync(Guid challengeId, string mobileE164, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
