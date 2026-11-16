using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanfiqo.Identity;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class RetryAfterTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Wait_is_zero_below_the_limit_and_until_the_oldest_blocking_send_leaves_the_window()
    {
        var window = TimeSpan.FromMinutes(15);
        Assert.Equal(TimeSpan.Zero, OtpRateLimitWait.For([Now.AddMinutes(-1), Now.AddMinutes(-2)], window, 3, Now));
        // Three sends in the window, limit three: the oldest (10 minutes ago) must leave, which happens in 5 minutes.
        Assert.Equal(TimeSpan.FromMinutes(5), OtpRateLimitWait.For([Now.AddMinutes(-10), Now.AddMinutes(-4), Now.AddMinutes(-1)], window, 3, Now));
        // Sends outside the window do not count.
        Assert.Equal(TimeSpan.Zero, OtpRateLimitWait.For([Now.AddMinutes(-20), Now.AddMinutes(-30), Now.AddMinutes(-1)], window, 3, Now));
        // Over the limit: the (count - limit + 1)th oldest has to leave.
        Assert.Equal(TimeSpan.FromMinutes(4), OtpRateLimitWait.For([Now.AddMinutes(-14), Now.AddMinutes(-11), Now.AddMinutes(-5), Now.AddMinutes(-1)], window, 3, Now));
    }

    [Fact]
    public void The_exception_never_asks_for_less_than_one_second()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), new OtpRateLimitException().RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), new OtpRateLimitException(TimeSpan.FromMilliseconds(-5)).RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(42), new OtpRateLimitException(TimeSpan.FromSeconds(42)).RetryAfter);
    }

    [Fact]
    public async Task Fixed_window_rejection_sends_retry_after_in_secondsAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApiRateLimiter();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/ping", () => "ok").RequireRateLimiting("auth");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        for (var i = 0; i < 10; i++) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ping")).StatusCode);
        var rejected = await client.GetAsync("/ping");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var seconds = int.Parse(rejected.Headers.GetValues("Retry-After").Single());
        Assert.InRange(seconds, 1, 60);
    }

    [Fact]
    public async Task Otp_request_over_the_limit_returns_429_with_the_exact_wait_in_secondsAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.RemoveAll<IMobileAuthentication>();
        builder.Services.AddSingleton<IMobileAuthentication>(new LimitedAuthentication(TimeSpan.FromSeconds(41.2)));
        builder.Services.AddSingleton<IAuthBusinessProfileReader, NoBusinessReader>();
        builder.Services.AddApiRateLimiter();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapSanfiqoAuthEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "+919999900001" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("42", response.Headers.GetValues("Retry-After").Single());
    }

    private sealed class NoBusinessReader : IAuthBusinessProfileReader
    {
        public Task<AuthBusinessProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<AuthBusinessProfile?>(null);
    }

    private sealed class LimitedAuthentication(TimeSpan wait) : IMobileAuthentication
    {
        public Task<Guid> RequestMobileCodeAsync(string phoneNumber, string clientIp, CancellationToken cancellationToken = default) => throw new OtpRateLimitException(wait);
        public Task<MobileAuthenticationResult> CompleteMobileSignInAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
