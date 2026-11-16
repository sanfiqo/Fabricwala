using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class ApiErrorTests
{
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "authentication_required")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    [InlineData(413, "payload_too_large")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(429, "rate_limited")]
    [InlineData(500, "internal_error")]
    [InlineData(503, "service_unavailable")]
    public async Task Http_errors_have_stable_codes_without_original_detailsAsync(int status, string code)
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.GetAsync($"/api/v1/status/{status}");
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(code, body!.Code);
        Assert.DoesNotContain("private diagnostic", await response.Content.ReadAsStringAsync());
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("30", response.Headers.RetryAfter!.ToString());
    }

    [Fact]
    public async Task Exceptions_and_missing_routes_use_the_same_contractAsync()
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var failure = await client.GetAsync("/api/v1/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Equal("internal_error", (await failure.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Code);
        Assert.DoesNotContain("private diagnostic", await failure.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync("/api/v1/missing");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not_found", (await missing.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Code);
        using var invalid = await client.GetAsync("/api/v1/invalid");
        var body = await invalid.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(["invalid_value"], body!.Fields!["quantity"]);
    }

    [Fact]
    public async Task Successful_files_and_json_are_not_rewrittenAsync()
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.Equal(new byte[256 * 1024], await client.GetByteArrayAsync("/api/v1/file"));
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/ok");
        Assert.True(json.GetProperty("ok").GetBoolean());
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseMiddleware<ApiErrorMiddleware>();
        app.MapGet("/api/v1/status/{status:int}", (int status, HttpContext context) =>
        {
            context.Response.Headers.RetryAfter = "30";
            return Results.Json(new { message = "private diagnostic" }, statusCode: status);
        });
        app.MapGet("/api/v1/failure", (Func<IResult>)(() => throw new InvalidOperationException("private diagnostic")));
        app.MapGet("/api/v1/invalid", (Func<IResult>)(() => throw new ValidationException([new ValidationFailure("quantity", "private diagnostic")])));
        app.MapGet("/api/v1/file", () => Results.File(new byte[256 * 1024], "application/octet-stream"));
        app.MapGet("/api/v1/ok", () => Results.Ok(new { ok = true }));
        await app.StartAsync();
        return app;
    }
}
