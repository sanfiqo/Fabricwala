using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sanfiqo.Identity;
using TextileConnect.Api;

namespace TextileConnect.Tests;

/// <summary>The second step of the admin sign-in: a code from an authenticator app, switched by Auth:AdminMfa.</summary>
public sealed class AdminMfaTests
{
    private const string Phone = "+919000000001";

    private sealed class Fixture(WebApplication app, HttpClient client, string userId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public string UserId { get; } = userId;
        public IServiceProvider Services => app.Services;
        public ValueTask DisposeAsync() { Client.Dispose(); return app.DisposeAsync(); }

        public static async Task<Fixture> StartAsync(string role, string mode)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:AdminMfa"] = mode });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
            var userId = Guid.NewGuid().ToString();
            builder.Services.AddSingleton<IMobileAuthentication>(new SignIn(userId));
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 1000; limiter.Window = TimeSpan.FromMinutes(1); });
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
                var user = new ApplicationUser { Id = userId, UserName = "test-user", FullName = "Test user", PhoneNumber = Phone, IsActive = true };
                Assert.True((await users.CreateAsync(user)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }
            await app.StartAsync();
            return new Fixture(app, new HttpClient(new HttpClientHandler { CookieContainer = new System.Net.CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) }, userId);
        }
    }

    private sealed class SignIn(string userId) : IMobileAuthentication
    {
        private readonly Guid challenge = Guid.NewGuid();
        public Task<Guid> RequestMobileCodeAsync(string mobile, string clientIp, CancellationToken cancellationToken = default) => Task.FromResult(challenge);
        public Task<MobileAuthenticationResult> CompleteMobileSignInAsync(Guid challengeId, string mobile, string code, CancellationToken cancellationToken = default)
            => Task.FromResult(challengeId == challenge && mobile == Phone && code == "123456"
                ? new MobileAuthenticationResult(MobileAuthenticationStatus.Authenticated, userId)
                : new MobileAuthenticationResult(MobileAuthenticationStatus.Rejected));
    }

    // ---- helpers --------------------------------------------------------------------------------------------------------------------

    private static async Task CsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    }

    /// <summary>The mobile one-time code step. Returns the profile.</summary>
    private static async Task<JsonElement> SignInAsync(HttpClient client)
    {
        await CsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = Phone })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = Phone, code = "123456" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await CsrfAsync(client);                                         // a new session needs a new token
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpStatusCode> AdminRouteAsync(HttpClient client) => client.GetAsync("/api/v1/admin/test-permission").ContinueWith(t => t.Result.StatusCode);

    /// <summary>What an authenticator app shows now: RFC 6238, HMAC-SHA1, 30 seconds, 6 digits, from the base32 secret.</summary>
    private static string Totp(string base32Key, int stepOffset = 0)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0; var value = 0; var bytes = new List<byte>();
        foreach (var c in base32Key.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | alphabet.IndexOf(c); bits += 5;
            if (bits >= 8) { bytes.Add((byte)(value >> (bits - 8))); bits -= 8; }
        }
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + stepOffset;
        var counter = BitConverter.GetBytes(step); if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        var offset = hash[^1] & 0x0F;
        var number = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (number % 1_000_000).ToString("D6");
    }

    private static async Task<(string Key, JsonElement Enabled)> EnrolAsync(HttpClient client)
    {
        var setup = await client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var body = await setup.Content.ReadFromJsonAsync<JsonElement>();
        var key = body.GetProperty("sharedKey").GetString()!;
        Assert.StartsWith("otpauth://totp/Fabricwala:", body.GetProperty("authenticatorUri").GetString());
        var enable = await client.PostAsJsonAsync("/api/v1/auth/mfa/enable", new { code = Totp(key) });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        return (key, await enable.Content.ReadFromJsonAsync<JsonElement>());
    }

    // ---- tests ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task With_the_step_off_an_admin_signs_in_as_before()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Off");
        var profile = await SignInAsync(fixture.Client);
        Assert.Equal("Admin", profile.GetProperty("roles")[0].GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("mfaPending").ValueKind);
        Assert.Equal(HttpStatusCode.OK, await AdminRouteAsync(fixture.Client));
    }

    [Theory]
    [InlineData("Retailer")]
    [InlineData("Manufacturer")]
    public async Task The_step_is_for_admins_only(string role)
    {
        await using var fixture = await Fixture.StartAsync(role, "Required");
        var profile = await SignInAsync(fixture.Client);
        Assert.Equal(role, profile.GetProperty("roles")[0].GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("mfaPending").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { })).StatusCode);     // not an admin: nothing to enrol
    }

    [Fact]
    public async Task A_new_admin_must_enrol_first_and_has_no_admin_rights_until_then()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        var profile = await SignInAsync(fixture.Client);

        Assert.Empty(profile.GetProperty("roles").EnumerateArray());
        Assert.Equal("enrol", profile.GetProperty("mfaPending").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));

        var (_, enabled) = await EnrolAsync(fixture.Client);
        Assert.Equal(10, enabled.GetProperty("recoveryCodes").GetArrayLength());
        Assert.Equal("Admin", enabled.GetProperty("profile").GetProperty("roles")[0].GetString());
        Assert.Equal(HttpStatusCode.OK, await AdminRouteAsync(fixture.Client));
        using var scope = fixture.Services.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(fixture.UserId))!.TwoFactorEnabled);
    }

    [Fact]
    public async Task An_enrolled_admin_needs_the_code_after_the_mobile_code()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        var (key, _) = await EnrolAsync(fixture.Client);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);

        var profile = await SignInAsync(fixture.Client);                  // the next day: mobile code again
        Assert.Equal("verify", profile.GetProperty("mfaPending").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));
        Assert.Empty((await fixture.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("roles").EnumerateArray());

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = "000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));
        var ok = await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = Totp(key) });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Admin", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("roles")[0].GetString());
        Assert.Equal(HttpStatusCode.OK, await AdminRouteAsync(fixture.Client));
    }

    [Fact]
    public async Task A_refresh_never_gives_the_admin_role_back_to_a_session_that_has_not_passed_the_step()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        await EnrolAsync(fixture.Client);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        await SignInAsync(fixture.Client);                                // pending: verify

        var refreshed = await fixture.Client.PostAsJsonAsync("/api/v1/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal("verify", (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mfaPending").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));
    }

    [Fact]
    public async Task A_security_stamp_change_does_not_hand_the_role_back_either()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        await EnrolAsync(fixture.Client);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        await SignInAsync(fixture.Client);                                // pending: verify
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.UpdateSecurityStampAsync((await users.FindByIdAsync(fixture.UserId))!);   // for example a role change or a reset
        }

        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));            // the cookie is renewed, still without the role
        Assert.Equal("verify", (await fixture.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("mfaPending").GetString());
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        var (_, enabled) = await EnrolAsync(fixture.Client);
        var code = enabled.GetProperty("recoveryCodes")[0].GetString()!;
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { });

        await SignInAsync(fixture.Client);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await AdminRouteAsync(fixture.Client));
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { });

        await SignInAsync(fixture.Client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code })).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_lock_the_user_for_a_while_even_for_the_right_code()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        var (key, _) = await EnrolAsync(fixture.Client);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        await SignInAsync(fixture.Client);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = "000000" })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = Totp(key) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));
    }

    [Fact]
    public async Task The_verify_route_is_only_for_a_session_that_is_waiting_for_it()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);                                // pending: enrol, there is no secret to verify against yet
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/enable", new { code = "123456" })).StatusCode);   // no setup yet
        using var anonymous = new HttpClient { BaseAddress = fixture.Client.BaseAddress };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { code = "123456" })).StatusCode);
    }

    [Fact]
    public async Task Switching_the_step_on_ends_old_admin_sessions_that_never_passed_it()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Off");
        await SignInAsync(fixture.Client);
        Assert.Equal(HttpStatusCode.OK, await AdminRouteAsync(fixture.Client));

        fixture.Services.GetRequiredService<IConfiguration>()["Auth:AdminMfa"] = "Required";    // the owner switches it on while an admin is signed in

        Assert.Equal(HttpStatusCode.Unauthorized, await AdminRouteAsync(fixture.Client));        // the old cookie is no longer accepted: sign in again, with the second step
        var profile = await SignInAsync(fixture.Client);
        Assert.Equal("enrol", profile.GetProperty("mfaPending").GetString());
    }

    [Fact]
    public async Task A_wrong_first_code_does_not_switch_the_step_on()
    {
        await using var fixture = await Fixture.StartAsync("Admin", "Required");
        await SignInAsync(fixture.Client);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/setup", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/mfa/enable", new { code = "000000" })).StatusCode);

        using var scope = fixture.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(fixture.UserId))!.TwoFactorEnabled);
        Assert.Equal(HttpStatusCode.Forbidden, await AdminRouteAsync(fixture.Client));
    }
}
