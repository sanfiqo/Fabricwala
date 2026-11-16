using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class FrontendCorsTests
{
    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://127.0.0.1:5173", true)]
    [InlineData("http://192.168.1.20:5173", true)]
    [InlineData("http://10.0.0.8:5173", true)]
    [InlineData("http://172.16.4.2:5173", true)]
    [InlineData("http://172.31.255.1:5173", true)]
    [InlineData("http://172.32.0.1:5173", false)]   // outside the private 172.16/12 range
    [InlineData("http://8.8.8.8:5173", false)]       // public address
    [InlineData("http://192.168.1.20:3000", false)]  // not the dev port
    [InlineData("https://192.168.1.20:5173", false)] // LAN access is plain http only
    [InlineData("http://evil.example:5173", false)]
    [InlineData("http://localhost.evil.example:5173", false)]
    [InlineData("http://localhost:5173/path", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    public void Development_allows_only_the_dev_server_on_local_and_private_addresses(string origin, bool expected)
        => Assert.Equal(expected, FrontendCors.IsAllowed(origin, development: true, []));

    [Fact]
    public void Outside_development_only_configured_origins_are_allowed()
    {
        Assert.False(FrontendCors.IsAllowed("http://localhost:5173", development: false, []));
        Assert.False(FrontendCors.IsAllowed("http://192.168.1.20:5173", development: false, []));
        Assert.True(FrontendCors.IsAllowed("https://app.example.com", development: false, ["https://app.example.com"]));
        Assert.True(FrontendCors.IsAllowed("https://APP.example.com", development: false, ["https://app.example.com"]));
        Assert.False(FrontendCors.IsAllowed("https://other.example.com", development: false, ["https://app.example.com"]));
    }

    [Fact]
    public void Configured_origins_accept_one_value_or_a_list_and_ignore_trailing_slashes()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:FrontendOrigin"] = "https://one.example.com/", ["Cors:FrontendOrigins:0"] = "https://two.example.com", ["Cors:FrontendOrigins:1"] = " "
        }).Build();
        Assert.Equal(new[] { "https://two.example.com", "https://one.example.com" }, FrontendCors.Configured(config));
    }

    private static async Task<(string? AllowOrigin, string? AllowCredentials)> PreflightAsync(string environment, string origin, string? configuredOrigin = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment, ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuredOrigin is null ? [] : new Dictionary<string, string?> { ["Cors:FrontendOrigin"] = configuredOrigin });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddFrontendCors(builder.Configuration, builder.Environment);
        await using var app = builder.Build();
        app.UseCors(FrontendCors.PolicyName);
        app.MapPost("/api/v1/ping", () => "pong");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/ping");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-csrf-token,content-type,idempotency-key");
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin);
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials);
        return (allowOrigin?.SingleOrDefault(), credentials?.SingleOrDefault());
    }

    [Fact]
    public async Task Preflight_from_the_dev_server_or_the_lan_is_allowed_with_credentials_in_developmentAsync()
    {
        foreach (var origin in new[] { "http://localhost:5173", "http://192.168.29.12:5173" })
        {
            var (allow, credentials) = await PreflightAsync("Development", origin);
            Assert.Equal(origin, allow);
            Assert.Equal("true", credentials);
        }
        Assert.Null((await PreflightAsync("Development", "http://evil.example:5173")).AllowOrigin);
    }

    [Fact]
    public async Task Preflight_in_production_ignores_localhost_and_honours_the_configured_originAsync()
    {
        Assert.Null((await PreflightAsync("Production", "http://localhost:5173")).AllowOrigin);
        Assert.Null((await PreflightAsync("Production", "http://192.168.29.12:5173")).AllowOrigin);
        Assert.Equal("https://app.example.com", (await PreflightAsync("Production", "https://app.example.com", "https://app.example.com")).AllowOrigin);
    }
}
