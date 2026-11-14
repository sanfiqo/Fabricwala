using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sanfiqo.Identity;
using TextileConnect.Api;

namespace TextileConnect.Tests;

// HTTP/cookie/CSRF/role contract tests. The mobile-auth boundary is substituted;
// these tests deliberately do not claim to exercise MySQL OTP/proof row locking.
public sealed class AuthEndpointTests
{
    [Theory]
    [InlineData("Retailer")]
    [InlineData("Manufacturer")]
    [InlineData("Admin")]
    public async Task Otp_cookie_refresh_roles_and_logout_work_over_httpAsync(string role)
    {
        await using var fixture = await Fixture.StartAsync(role);
        using var client = fixture.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { })).StatusCode);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = Fixture.Phone })).StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = Fixture.Phone, code = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var verified = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = Fixture.Phone, code = "123456" });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var profile = await verified.Content.ReadFromJsonAsync<AuthProfileResponse>();
        Assert.Equal([role], profile!.Roles);
        Assert.Equal("Test user", profile.FullName);
        Assert.Equal("en", profile.PreferredLanguage);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(role == "Admin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/admin/test-permission")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { })).StatusCode);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_csrf_and_rate_limit_use_error_contractAsync()
    {
        await using var fixture = await Fixture.StartAsync("Retailer");
        using var client = fixture.Client;
        var csrf = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = Fixture.Phone });
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal("validation_failed", (await csrf.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Code);
        await SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = Fixture.Phone, code = "x" })).StatusCode);
        HttpResponseMessage? response = null;
        for (var index = 0; index < 12; index++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "invalid" });
        }
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
            Assert.Equal("rate_limited", (await response.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Code);
        }
    }

    private static async Task SetCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    }

    private sealed class Fixture(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public const string Phone = "+919000000001";
        public HttpClient Client { get; } = client;
        public ValueTask DisposeAsync() => app.DisposeAsync();

        public static async Task<Fixture> StartAsync(string role)
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
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
            builder.Services.AddSingleton<IAuthBusinessProfileReader, EmptyBusinessReader>();
            var authentication = new ContractAuthentication();
            builder.Services.AddSingleton<IMobileAuthentication>(authentication);
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); });
            });
            var app = builder.Build();
            app.UseMiddleware<ApiErrorMiddleware>();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ApiCsrfMiddleware>();
            app.UseAntiforgery();
            app.MapSanfiqoAuthEndpoints();
            app.MapGet("/api/v1/admin/test-permission", () => Results.Ok()).RequireAuthorization(policy => policy.RequireRole("Admin"));
            await SanfiqoRoleSeeder.SeedAsync(app.Services);
            using (var scope = app.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser { Id = authentication.UserId, UserName = "test-user", FullName = "Test user", PhoneNumber = Phone, IsActive = true };
                Assert.True((await users.CreateAsync(user)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }
            await app.StartAsync();
            return new Fixture(app, new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) });
        }
    }

    private sealed class EmptyBusinessReader : IAuthBusinessProfileReader
    {
        public Task<AuthBusinessProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<AuthBusinessProfile?>(null);
    }

    private sealed class ContractAuthentication : IMobileAuthentication
    {
        private readonly Guid challenge = Guid.NewGuid();
        public string UserId { get; } = Guid.NewGuid().ToString();
        public Task<Guid> RequestMobileCodeAsync(string mobile, string clientIp, CancellationToken cancellationToken = default) => Task.FromResult(challenge);
        public Task<MobileAuthenticationResult> CompleteMobileSignInAsync(Guid challengeId, string mobile, string code, CancellationToken cancellationToken = default)
            => Task.FromResult(challengeId == challenge && mobile == Fixture.Phone && code == "123456"
                ? new MobileAuthenticationResult(MobileAuthenticationStatus.Authenticated, UserId)
                : new MobileAuthenticationResult(MobileAuthenticationStatus.Rejected));
    }
}
