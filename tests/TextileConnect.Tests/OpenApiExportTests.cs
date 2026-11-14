using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Sanfiqo.Identity;
using TextileConnect.Api;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Files;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Payments;

namespace TextileConnect.Tests;

/// <summary>
/// Keeps docs/openapi.json in step with the real endpoints and DTO names. When an endpoint or DTO changes this test fails;
/// run it once with OPENAPI_EXPORT=1 to rewrite the file, then commit the new file. The document is built from the
/// route table of a test host (no database, no secrets), merged from the marketplace routes and the sign-in routes.
/// </summary>
public sealed class OpenApiExportTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TextileConnect.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }

    private static async Task<JsonNode> MarketplaceSpecAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app =>
        {
            app.UseSwagger();
            app.MapAdminEndpoints(); app.MapProductEndpoints(); app.MapLandedCostEndpoints(); app.MapInvoiceEndpoints(); app.MapReturnClaimEndpoints();
            app.MapNotificationEndpoints(); app.MapAccountEndpoints(); app.MapStorefrontEndpoints(); app.MapSellerDashboardEndpoints(); app.MapBulkProductEndpoints();
            app.MapStockAlertEndpoints(); app.MapPayoutEndpoints(); app.MapAdminDashboardEndpoints(); app.MapAdminReportEndpoints(); app.MapProductModerationEndpoints();
            app.MapCategoryBannerEndpoints(); app.MapBusinessSuspensionEndpoints(); app.MapAdminListEndpoints(); app.MapRetailerExtrasEndpoints(); app.MapCartEndpoints(); app.MapOrderEndpoints(); app.MapPaymentEndpoints(); app.MapCommunityEndpoints();
        }, builder =>
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddScoped<IFinanceService, EfFinanceService>();
            builder.Services.AddScoped<TextileConnect.Application.Community.ICommunityService, TextileConnect.Infrastructure.Community.EfCommunityService>();
        });
        using var client = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    /// <summary>Sign-in routes (send OTP, verify, refresh, logout, profile) from the identity library, on a host with in-memory identity data.</summary>
    private static async Task<JsonNode> AuthSpecAsync()
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
        builder.Services.AddSingleton<IAuthBusinessProfileReader, NoBusinessReader>();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); }));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        await using var app = builder.Build();
        app.UseSwagger();
        app.MapSanfiqoAuthEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private sealed class NoBusinessReader : IAuthBusinessProfileReader
    {
        public Task<AuthBusinessProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<AuthBusinessProfile?>(null);
    }

    private static void Merge(JsonNode target, JsonNode source)
    {
        foreach (var (key, value) in source["paths"]!.AsObject().ToList())
            if (!target["paths"]!.AsObject().ContainsKey(key)) target["paths"]![key] = value!.DeepClone();
        var schemas = source["components"]?["schemas"]?.AsObject();
        if (schemas is null) return;
        target["components"] ??= new JsonObject();
        target["components"]!["schemas"] ??= new JsonObject();
        foreach (var (key, value) in schemas.ToList())
            if (!target["components"]!["schemas"]!.AsObject().ContainsKey(key)) target["components"]!["schemas"]![key] = value!.DeepClone();
    }

    [Fact]
    public async Task OpenApi_document_matches_the_endpoints_and_is_exported_on_requestAsync()
    {
        var spec = await MarketplaceSpecAsync();
        Merge(spec, await AuthSpecAsync());
        var paths = spec["paths"]!.AsObject();
        Assert.True(paths.Count > 60, "Expected the full marketplace route table.");
        foreach (var required in new[] { "/api/v1/checkout", "/api/v1/cart", "/api/v1/orders", "/api/v1/seller/orders", "/api/v1/seller/products", "/api/v1/seller/payouts",
                     "/api/v1/products", "/api/v1/auth/otp/request", "/api/v1/notifications", "/api/v1/account", "/api/v1/admin/dashboard", "/api/v1/payments" })
            Assert.True(paths.ContainsKey(required), required);

        spec["info"]!["title"] = "TextileConnect API";
        spec["info"]!["version"] = "v1";
        NameAnonymousSchemas(spec);
        var text = Canonical(spec);
        var file = Path.Combine(RepoRoot(), "docs", "openapi.json");
        if (Environment.GetEnvironmentVariable("OPENAPI_EXPORT") == "1")
        {
            File.WriteAllText(file, text);
            return;
        }
        Assert.True(File.Exists(file), "docs/openapi.json is missing. Run this test once with OPENAPI_EXPORT=1.");
        Assert.True(File.ReadAllText(file).Replace("\r\n", "\n") == text, "docs/openapi.json is out of date. Run this test with OPENAPI_EXPORT=1 and commit the file.");
    }

    /// <summary>
    /// Handlers that return anonymous objects get compiler-numbered schema names that change whenever any anonymous type is added anywhere.
    /// They are renamed AnonymousResponse1..n in a stable order (by their content) so the exported file only changes when an API really changes.
    /// </summary>
    private static void NameAnonymousSchemas(JsonNode spec)
    {
        var schemas = spec["components"]?["schemas"]?.AsObject();
        if (schemas is null) return;
        var anonymous = schemas.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.Key, @"f__AnonymousType\d+$"))
            .Select(x => (x.Key, Body: x.Value!.ToJsonString())).OrderBy(x => x.Body, StringComparer.Ordinal).ToList();
        var rename = new Dictionary<string, string>();
        for (var i = 0; i < anonymous.Count; i++) rename[anonymous[i].Key] = $"AnonymousResponse{i + 1}";
        foreach (var (oldName, _) in anonymous)
        {
            var body = schemas[oldName];
            schemas.Remove(oldName);
            schemas[rename[oldName]] = body;
        }
        Rewrite(spec, rename);
    }

    private static void Rewrite(JsonNode? node, Dictionary<string, string> rename)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(x => x.Key).ToList())
                    if (key == "$ref" && obj[key] is JsonValue value && value.TryGetValue<string>(out var reference) && reference.StartsWith("#/components/schemas/"))
                    {
                        var name = reference["#/components/schemas/".Length..];
                        if (rename.TryGetValue(name, out var renamed)) obj[key] = "#/components/schemas/" + renamed;
                    }
                    else Rewrite(obj[key], rename);
                break;
            case JsonArray array:
                foreach (var item in array) Rewrite(item, rename);
                break;
        }
    }

    /// <summary>Stable, readable JSON: sorted keys, two-space indent, LF line endings, trailing newline.</summary>
    private static string Canonical(JsonNode node) => JsonSerializer.Serialize(Sort(node), new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => KeyValuePair.Create(x.Key, Sort(x.Value)))),
        JsonArray array => new JsonArray(array.Select(x => Sort(x)).ToArray()),
        null => null,
        _ => node.DeepClone()
    };
}
