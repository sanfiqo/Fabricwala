using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanfiqo.Identity;
using TextileConnect.Api;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Application.Catalogue;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Infrastructure.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

// Isolated HTTP endpoint fixture: test-only authentication and EF in-memory storage.
// The real cookie/CSRF login contract is covered separately by AuthEndpointTests.
public sealed class MarketplaceHttpFixture(WebApplication app) : IAsyncDisposable
{
    public IServiceProvider Services => app.Services;
    public TestClock Clock => (TestClock)app.Services.GetRequiredService<TimeProvider>();
    public ValueTask DisposeAsync() => app.DisposeAsync();

    public static async Task<MarketplaceHttpFixture> StartAsync(Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<MarketplaceDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(database + "-identity"));
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationIdentityDbContext>();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("ApprovedBusiness", policy => policy.RequireAuthenticatedUser().AddRequirements(new ApprovedBusinessRequirement())));
        builder.Services.AddSingleton<IAuthorizationHandler, ApprovedBusinessHandler>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentBusiness, CurrentBusiness>();
        builder.Services.AddScoped<IBusinessDirectory, EfBusinessDirectory>();
        builder.Services.AddScoped<IProductPricing, EfProductPricing>();
        builder.Services.AddScoped<ISellerProductService, EfSellerProductService>();
        builder.Services.AddScoped<IProductDirectory, EfProductDirectory>();
        builder.Services.AddScoped<TextileConnect.Application.Orders.ICartService, TextileConnect.Infrastructure.Orders.EfCartService>();
        builder.Services.AddScoped<TextileConnect.Application.Orders.ICheckoutService, TextileConnect.Infrastructure.Orders.EfCheckoutService>();
        builder.Services.AddScoped<TextileConnect.Application.Orders.IOrderService, TextileConnect.Infrastructure.Orders.EfOrderService>();
        builder.Services.AddScoped<TextileConnect.Application.Orders.IOrderMaintenance, TextileConnect.Infrastructure.Orders.EfOrderService>();
        builder.Services.AddSingleton<TimeProvider, TestClock>();
        builder.Services.AddSingleton<TextileConnect.Application.Payments.IPaymentGateway, TextileConnect.Application.Payments.DevelopmentPaymentGateway>();
        builder.Services.AddScoped<TextileConnect.Application.Payments.IPaymentService, TextileConnect.Infrastructure.Payments.EfPaymentService>();
        builder.Services.AddSingleton<IFileStorage, MemoryFileStorage>();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("search", limiter => { limiter.PermitLimit = 60; limiter.Window = TimeSpan.FromMinutes(1); });
        });
        var app = builder.Build();
        app.UseMiddleware<ApiErrorMiddleware>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ApiCsrfMiddleware>();
        app.UseAntiforgery();
        app.MapGet("/api/v1/test/csrf", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, Microsoft.AspNetCore.Http.HttpContext context) => new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        app.MapBusinessEndpoints();
        app.MapPincodeEndpoints();
        map?.Invoke(app);
        await SanfiqoRoleSeeder.SeedAsync(app.Services);
        using (var scope = app.Services.CreateScope()) await MarketplaceSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>());
        await app.StartAsync();
        return new(app);
    }

    public async Task<HttpClient> ClientAsync(string? role, string userId = "test-user", long? businessId = null, bool approved = true)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        if (role is not null)
        {
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByIdAsync(userId) is null)
            {
                var user = new ApplicationUser { Id = userId, UserName = userId, PhoneNumber = "+919000000001", IsActive = true };
                Assert.True((await users.CreateAsync(user)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }
            client.DefaultRequestHeaders.Add("X-Test-User", userId);
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            if (businessId.HasValue) client.DefaultRequestHeaders.Add("X-Test-Business", businessId.Value.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Approved", approved ? "true" : "false");
        }
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/test/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Headers["X-Test-User"].ToString();
            if (userId.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Business", out var businessId)) claims.Add(new("business_id", businessId.ToString()));
            claims.Add(new("business_approved", Request.Headers["X-Test-Approved"].ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> files = new();
        public async Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var key = Guid.NewGuid().ToString("N");
            files[key] = buffer.ToArray();
            return key;
        }
        public Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(files[fileKey], writable: false));
    }
}
