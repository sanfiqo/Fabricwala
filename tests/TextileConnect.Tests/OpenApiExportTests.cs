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

    private static async Task<JsonNode> MarketplaceSpecAsync(List<RouteInfo> routes)
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app =>
        {
            app.UseSwagger();
            app.MapAdminEndpoints(); app.MapFeatureEndpoints(); app.MapRfqEndpoints(); app.MapInvoiceRegisterEndpoints(); app.MapProductEndpoints(); app.MapLandedCostEndpoints(); app.MapInvoiceEndpoints(); app.MapReturnClaimEndpoints();
            app.MapNotificationEndpoints(); app.MapAccountEndpoints(); app.MapStorefrontEndpoints(); app.MapSellerDashboardEndpoints(); app.MapBulkProductEndpoints();
            app.MapStockAlertEndpoints(); app.MapPayoutEndpoints(); app.MapAdminDashboardEndpoints(); app.MapAdminReportEndpoints(); app.MapProductModerationEndpoints();
            app.MapCategoryBannerEndpoints(); app.MapBusinessSuspensionEndpoints(); app.MapAdminListEndpoints(); app.MapRetailerExtrasEndpoints(); app.MapCartEndpoints(); app.MapOrderEndpoints(); app.MapPaymentEndpoints(); app.MapHealthEndpoints();
        }, builder =>
        {
            builder.Services.AddAppHealthChecks();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddScoped<IFinanceService, EfFinanceService>();
        });
        routes.AddRange(RoutesOf(fixture.Services));
        using var client = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    /// <summary>Sign-in routes (send OTP, verify, refresh, logout, profile) from the identity library, on a host with in-memory identity data.</summary>
    private static async Task<JsonNode> AuthSpecAsync(List<RouteInfo> routes)
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
        routes.AddRange(RoutesOf(app.Services));
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
        var routes = new List<RouteInfo>();
        var spec = await MarketplaceSpecAsync(routes);
        Merge(spec, await AuthSpecAsync(routes));
        var paths = spec["paths"]!.AsObject();
        Assert.True(paths.Count > 60, "Expected the full marketplace route table.");
        foreach (var required in new[] { "/api/v1/checkout", "/api/v1/cart", "/api/v1/orders", "/api/v1/seller/orders", "/api/v1/seller/products", "/api/v1/seller/payouts",
                     "/api/v1/products", "/api/v1/auth/otp/request", "/api/v1/notifications", "/api/v1/account", "/api/v1/admin/dashboard", "/api/v1/payments" })
            Assert.True(paths.ContainsKey(required), required);

        spec["info"]!["title"] = "TextileConnect API";
        spec["info"]!["version"] = "v1";
        NameAnonymousSchemas(spec);
        var text = Canonical(spec);
        var reference = ApiReference(spec, routes);
        var file = Path.Combine(RepoRoot(), "docs", "openapi.json");
        var referenceFile = Path.Combine(RepoRoot(), "docs", "API_REFERENCE.md");
        if (Environment.GetEnvironmentVariable("OPENAPI_EXPORT") == "1")
        {
            File.WriteAllText(file, text);
            File.WriteAllText(referenceFile, reference);
            return;
        }
        Assert.True(File.Exists(file), "docs/openapi.json is missing. Run this test once with OPENAPI_EXPORT=1.");
        Assert.True(File.ReadAllText(file).Replace("\r\n", "\n") == text, "docs/openapi.json is out of date. Run this test with OPENAPI_EXPORT=1 and commit the file.");
        Assert.True(File.Exists(referenceFile) && File.ReadAllText(referenceFile).Replace("\r\n", "\n") == reference, "docs/API_REFERENCE.md is out of date. Run this test with OPENAPI_EXPORT=1 and commit the file.");
    }

    // ------------------------------------------------------------------ docs/API_REFERENCE.md

    private sealed record RouteInfo(string Method, string Path, string Access, string RateLimit);

    /// <summary>Every route of the host with who may call it and which rate limit applies, read from the endpoint metadata.</summary>
    private static IEnumerable<RouteInfo> RoutesOf(IServiceProvider services)
    {
        foreach (var endpoint in services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>())
        {
            var raw = endpoint.RoutePattern.RawText ?? string.Empty;
            if (!raw.StartsWith("/api/v1", StringComparison.Ordinal) || raw.StartsWith("/api/v1/test", StringComparison.Ordinal)) continue;
            var path = System.Text.RegularExpressions.Regex.Replace(raw, @"\{([A-Za-z0-9_]+)[^}]*\}", "{$1}").TrimEnd('/');
            var roles = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.AuthorizationPolicy>()
                .SelectMany(x => x.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>()).SelectMany(x => x.AllowedRoles).Distinct().ToList();
            var authorize = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            var anonymous = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null;
            var approved = authorize.Any(x => x.Policy == "ApprovedBusiness");
            var access = anonymous || authorize.Count == 0 ? "public"
                : (roles.Count > 0 ? string.Join(" or ", roles) : "any signed-in user") + (approved ? ", approved business" : string.Empty);
            var limit = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName ?? string.Empty;
            foreach (var method in endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                yield return new RouteInfo(method, path, access, limit);
        }
    }

    private static string SchemaName(JsonNode? schema)
    {
        if (schema is null) return string.Empty;
        if (schema["$ref"]?.GetValue<string>() is { } reference) return reference[(reference.LastIndexOf('/') + 1)..];
        if (schema["type"]?.GetValue<string>() == "array") return SchemaName(schema["items"]) is { Length: > 0 } item ? item + "[]" : "list";
        return schema["type"]?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>The table of docs/API_REFERENCE.md: one row per route, grouped by area. Made from the endpoint metadata and docs/openapi.json, so it cannot drift from the code.</summary>
    private static string ApiReference(JsonNode spec, List<RouteInfo> routes)
    {
        var text = new System.Text.StringBuilder();
        text.Append("# API reference\n\n");
        text.Append("Generated by `OpenApiExportTests` from the real routes and `docs/openapi.json`; do not edit by hand. Run that test with `OPENAPI_EXPORT=1` after a change.\n");
        text.Append("All routes are under `/api/v1`. Who may call: *public* needs no sign-in; otherwise the role (and an approved business where it says so). `limit` is the rate limit group (`auth`, `search`); every client address is also held to a general limit.\n");
        text.Append("Errors are always `{ \"code\": \"...\", \"message\": \"...\", \"fields\": { ... } }`. Request and response shapes are the schemas named here, in `docs/openapi.json`. Empty means the route has no typed body or returns plain data.\n");
        var groups = routes.DistinctBy(r => (r.Method, r.Path)).OrderBy(r => r.Path, StringComparer.Ordinal).ThenBy(r => r.Method, StringComparer.Ordinal)
            .GroupBy(r => r.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(2).FirstOrDefault() ?? "root");
        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.Append($"\n## {group.Key}\n\n| Method | Path | Who may call | Limit | Body | Returns |\n| --- | --- | --- | --- | --- | --- |\n");
            foreach (var route in group)
            {
                var operation = spec["paths"]?[route.Path]?[route.Method.ToLowerInvariant()];
                var content = operation?["requestBody"]?["content"]?.AsObject();
                var body = content is null ? string.Empty : content.ContainsKey("multipart/form-data") ? "file upload" : SchemaName(content.FirstOrDefault().Value?["schema"]);
                var success = operation?["responses"]?.AsObject().FirstOrDefault(x => x.Key.StartsWith('2')).Value?["content"]?.AsObject().FirstOrDefault().Value?["schema"];
                text.Append($"| {route.Method} | `{route.Path}` | {route.Access} | {route.RateLimit} | {body} | {SchemaName(success)} |\n");
            }
        }
        return text.ToString();
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
