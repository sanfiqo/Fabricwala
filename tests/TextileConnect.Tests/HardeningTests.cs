using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using TextileConnect.Api;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Rate limits per client, security headers, trusted proxies and the health probes, each in a small host of its own.</summary>
public sealed class HardeningTests
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(Action<WebApplicationBuilder>? services = null, Action<WebApplication>? pipeline = null, Action<WebApplication>? routes = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        services?.Invoke(builder);
        var app = builder.Build();
        pipeline?.Invoke(app);
        routes?.Invoke(app);
        await app.StartAsync();
        return (app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) });
    }

    /// <summary>A test-only middleware: the request claims to come from the address in X-Test-Ip, like a client at another address would.</summary>
    private static void PretendAddress(WebApplication app) => app.Use(async (context, next) =>
    {
        if (context.Request.Headers.TryGetValue("X-Test-Ip", out var ip)) context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
        await next();
    });

    private static HttpRequestMessage From(string ip, string path = "/ping", HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Add("X-Test-Ip", ip);
        return request;
    }

    // ---------------------------------------------------------------- rate limits

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]                       // an IPv4 address written the IPv6 way is the same client
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:ffff:ffff:ffff:ffff", "2001:db8:1:2::/64")]   // the same /64 network is one client
    public void A_client_is_counted_by_its_address_and_an_ipv6_network_counts_once(string address, string key)
        => Assert.Equal(key, ApiRateLimiting.ClientKey(IPAddress.Parse(address)));

    [Fact]
    public void A_request_without_an_address_is_still_counted()
        => Assert.Equal("unknown", ApiRateLimiting.ClientKey((IPAddress?)null));

    [Fact]
    public async Task One_client_using_up_its_allowance_does_not_block_anybody_elseAsync()
    {
        var (app, client) = await StartAsync(b => b.Services.AddApiRateLimiter(), a => { PretendAddress(a); a.UseRateLimiter(); }, a => a.MapGet("/ping", () => "ok").RequireRateLimiting("auth"));
        await using var _ = app; using var __ = client;

        for (var i = 0; i < ApiRateLimiting.AuthPerMinute; i++) Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(From("203.0.113.10"))).StatusCode);
        var blocked = await client.SendAsync(From("203.0.113.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.InRange(int.Parse(blocked.Headers.GetValues("Retry-After").Single()), 1, 60);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(From("203.0.113.11"))).StatusCode);   // another shop, another allowance
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(From("2001:db8::1"))).StatusCode);
    }

    [Fact]
    public async Task The_overall_backstop_stops_a_client_that_hammers_the_api_but_not_the_health_probesAsync()
    {
        var (app, client) = await StartAsync(b => b.Services.AddApiRateLimiter(), a => { PretendAddress(a); a.UseRateLimiter(); },
            a => { a.MapGet("/api/v1/anything", () => "ok"); a.MapGet("/api/v1/health", () => "ok"); });
        await using var _ = app; using var __ = client;

        for (var i = 0; i < ApiRateLimiting.AnyRequestPerMinute; i++) await client.SendAsync(From("203.0.113.20", "/api/v1/anything"));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(From("203.0.113.20", "/api/v1/anything"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(From("203.0.113.21", "/api/v1/anything"))).StatusCode);
        for (var i = 0; i < 20; i++) Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(From("203.0.113.20", "/api/v1/health"))).StatusCode); // a probe is never counted
    }

    // ---------------------------------------------------------------- headers

    [Fact]
    public async Task Every_api_answer_carries_the_security_headers_and_private_answers_are_not_cachedAsync()
    {
        var (app, client) = await StartAsync(null, a =>
        {
            a.UseMiddleware<SecurityHeadersMiddleware>();
            a.Use(async (context, next) => { if (context.Request.Headers.ContainsKey("X-Signed-In")) context.User = new ClaimsPrincipal(new ClaimsIdentity("test")); await next(); });
        }, a =>
        {
            a.MapGet("/api/v1/public", () => "ok");
            a.MapPost("/api/v1/change", () => "ok");
            a.MapGet("/swagger/page", () => "page");
            a.MapGet("/api/v1/own-cache", (HttpContext c) => { c.Response.Headers.CacheControl = "public, max-age=60"; return "ok"; });
        });
        await using var _ = app; using var __ = client;

        var anonymous = await client.GetAsync("/api/v1/public");
        Assert.Equal("nosniff", anonymous.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", anonymous.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", anonymous.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", anonymous.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("camera=()", anonymous.Headers.GetValues("Permissions-Policy").Single());
        Assert.False(anonymous.Headers.Contains("Cache-Control"));   // a public read may be cached

        Assert.Equal("no-store", (await client.PostAsync("/api/v1/change", null)).Headers.CacheControl!.ToString());
        using var signedIn = new HttpRequestMessage(HttpMethod.Get, "/api/v1/public");
        signedIn.Headers.Add("X-Signed-In", "1");
        Assert.Equal("no-store", (await client.SendAsync(signedIn)).Headers.CacheControl!.ToString());

        var swagger = await client.GetAsync("/swagger/page");
        Assert.True(swagger.Headers.Contains("X-Content-Type-Options"));
        Assert.False(swagger.Headers.Contains("Content-Security-Policy")); // Swagger needs scripts, and it is Development only
        Assert.Equal("public, max-age=60", (await client.GetAsync("/api/v1/own-cache")).Headers.CacheControl!.ToString()); // a cache rule set by an endpoint is kept
    }

    // ---------------------------------------------------------------- trusted proxies

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();

    [Fact]
    public void Without_a_list_of_proxies_the_forwarded_headers_are_ignored()
        => Assert.Null(ForwardedHeadersSetup.Options(Config()));

    [Fact]
    public async Task The_forwarded_address_is_used_only_when_the_caller_is_a_trusted_proxyAsync()
    {
        var trusted = ForwardedHeadersSetup.Options(Config(("Proxy:KnownNetworks:0", "127.0.0.0/8"), ("Proxy:KnownNetworks:1", "::1/128")))!;
        var (withProxy, clientA) = await StartAsync(null, a => a.UseForwardedHeaders(trusted), a => a.MapGet("/ip", (HttpContext c) => c.Connection.RemoteIpAddress!.ToString()));
        var (withoutProxy, clientB) = await StartAsync(null, null, a => a.MapGet("/ip", (HttpContext c) => c.Connection.RemoteIpAddress!.MapToIPv4().ToString()));
        await using var _ = withProxy; await using var __ = withoutProxy; using var ___ = clientA; using var ____ = clientB;

        using var forged = new HttpRequestMessage(HttpMethod.Get, "/ip");
        forged.Headers.Add("X-Forwarded-For", "198.51.100.77");
        Assert.Equal("198.51.100.77", await (await clientA.SendAsync(forged)).Content.ReadAsStringAsync());   // trusted: the real client

        using var forgedAgain = new HttpRequestMessage(HttpMethod.Get, "/ip");
        forgedAgain.Headers.Add("X-Forwarded-For", "198.51.100.77");
        Assert.Equal("127.0.0.1", await (await clientB.SendAsync(forgedAgain)).Content.ReadAsStringAsync());   // no proxy configured: the header is just ignored
    }

    // ---------------------------------------------------------------- health

    private sealed class FakeStorage(bool healthy) : IFileStorage
    {
        public Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default) => Task.FromResult("key");
        public Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(healthy);
    }

    private static Task<(WebApplication App, HttpClient Client)> HealthHostAsync(bool storageHealthy)
        => StartAsync(b =>
        {
            b.Services.AddDbContext<MarketplaceDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            b.Services.AddSingleton<IFileStorage>(new FakeStorage(storageHealthy));
            b.Services.AddAppHealthChecks();
        }, null, a => a.MapHealthEndpoints());

    [Fact]
    public async Task Ready_means_the_database_and_the_storage_work_and_live_only_means_the_process_runsAsync()
    {
        var (app, client) = await HealthHostAsync(storageHealthy: true);
        await using var _ = app; using var __ = client;
        foreach (var path in new[] { "/api/v1/health", "/api/v1/health/live" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        var ready = await client.GetAsync("/api/v1/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = JsonDocument.Parse(await ready.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("storage").GetString());
    }

    [Fact]
    public async Task A_broken_storage_makes_the_app_not_ready_but_still_aliveAsync()
    {
        var (app, client) = await HealthHostAsync(storageHealthy: false);
        await using var _ = app; using var __ = client;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health/live")).StatusCode);   // restarting would not help
        var ready = await client.GetAsync("/api/v1/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var body = JsonDocument.Parse(await ready.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("storage").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task An_unreachable_database_is_unhealthy_quickly_and_the_answer_does_not_name_the_serverAsync()
    {
        await using var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseMySQL("Server=127.0.0.1;Port=1;Database=nothing;Uid=nobody;Pwd=nothing;Connection Timeout=1").Options);
        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("127.0.0.1", result.Description);
        Assert.Null(result.Exception);
    }
}
