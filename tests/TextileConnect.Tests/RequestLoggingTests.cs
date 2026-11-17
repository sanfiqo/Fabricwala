using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class RequestLoggingTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) { lock (Events) Events.Add(logEvent); }
        /// <summary>Everything a log tool would show for the event: the message, every property and the exception.</summary>
        public static string Everything(LogEvent e) => e.RenderMessage() + " | " + string.Join(" | ", e.Properties.Select(p => $"{p.Key}={p.Value}")) + " | " + e.Exception;
    }

    private static async Task<(WebApplication App, HttpClient Client, CollectingSink Sink)> StartAsync()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseSerilog((_, configuration) => configuration.MinimumLevel.Information().Enrich.FromLogContext().KeepSecretsOut().WriteTo.Sink(sink));
        var app = builder.Build();
        app.UseApiRequestLogging();
        app.UseMiddleware<ApiErrorMiddleware>();
        app.MapGet("/api/v1/echo", () => "ok");
        app.MapPost("/api/v1/otp/verify", () => "ok");
        app.MapGet("/api/v1/health", () => "ok");
        app.MapGet("/api/v1/boom", (HttpContext _) => { throw new InvalidOperationException("SECRET-EXCEPTION-TEXT code 123456 for +919999900001"); });
        await app.StartAsync();
        return (app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }, sink);
    }

    private static List<LogEvent> RequestLines(CollectingSink sink) => sink.Events.Where(e => e.MessageTemplate.Text.StartsWith("{RequestMethod", StringComparison.Ordinal)).ToList();

    [Fact]
    public async Task Every_request_gets_a_correlation_id_that_is_in_the_header_and_in_its_log_linesAsync()
    {
        var (app, client, sink) = await StartAsync();
        await using var _ = app; using var __ = client;

        var generated = await client.GetAsync("/api/v1/echo");
        var id = generated.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Equal(id, RequestLines(sink).Single().Properties["CorrelationId"].ToString().Trim('"'));

        using var withOwn = new HttpRequestMessage(HttpMethod.Get, "/api/v1/echo");
        withOwn.Headers.Add("X-Correlation-Id", "order-screen.1234");
        Assert.Equal("order-screen.1234", (await client.SendAsync(withOwn)).Headers.GetValues("X-Correlation-Id").Single());

        foreach (var bad in new[] { "short", "has spaces in it!", new string('x', 65), "line\tbreak-xxxxxxxx" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/echo");
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", bad);
            var answered = (await client.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single();
            Assert.NotEqual(bad, answered);
            Assert.Matches("^[0-9a-f]{32}$", answered);
        }
    }

    [Fact]
    public async Task A_failure_is_logged_once_as_an_error_with_the_same_correlation_idAsync()
    {
        var (app, client, sink) = await StartAsync();
        await using var _ = app; using var __ = client;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/boom");
        request.Headers.Add("X-Correlation-Id", "support-case-0001");
        var response = await request.SendOnAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("support-case-0001", response.Headers.GetValues("X-Correlation-Id").Single());
        var errors = sink.Events.Where(e => e.Level == LogEventLevel.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Equal("\"support-case-0001\"", e.Properties["CorrelationId"].ToString()));
        Assert.Contains(errors, e => e.RenderMessage().Contains("InvalidOperationException") && e.RenderMessage().Contains("support-case-0001")); // the error line names the kind of failure and the id
    }

    [Fact]
    public async Task One_time_codes_mobile_numbers_tokens_and_exception_text_never_reach_the_logsAsync()
    {
        var (app, client, sink) = await StartAsync();
        await using var _ = app; using var __ = client;

        using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/v1/otp/verify?code=123456&phone=%2B919999900001")
        { Content = new StringContent("""{"phoneNumber":"+919999900001","code":"123456","note":"SECRET-BODY-TEXT"}""", Encoding.UTF8, "application/json") };
        verify.Headers.Add("Authorization", "Bearer SECRET-TOKEN-XYZ");
        verify.Headers.Add("Cookie", "tc_session=SECRET-COOKIE-ABC; tc_otp_challenge=SECRET-CHALLENGE");
        await client.SendAsync(verify);
        await client.GetAsync("/api/v1/boom?phone=%2B919999900001");

        var everything = string.Join("\n", sink.Events.Select(CollectingSink.Everything));
        Assert.NotEmpty(sink.Events);
        foreach (var secret in new[] { "123456", "919999900001", "SECRET-BODY-TEXT", "SECRET-TOKEN-XYZ", "SECRET-COOKIE-ABC", "SECRET-CHALLENGE", "SECRET-EXCEPTION-TEXT" })
            Assert.DoesNotContain(secret, everything);
        // What is logged: the method, the path without the query, the status and the time.
        var line = RequestLines(sink).First();
        Assert.Equal("POST /api/v1/otp/verify answered 200", string.Join(' ', line.RenderMessage().Split(' ').Take(4)));
    }

    [Fact]
    public async Task Health_probes_are_not_logged_while_they_succeed()
    {
        var (app, client, sink) = await StartAsync();
        await using var _ = app; using var __ = client;
        for (var i = 0; i < 5; i++) await client.GetAsync("/api/v1/health");
        await client.GetAsync("/api/v1/echo");
        Assert.Single(RequestLines(sink));
        Assert.Equal("\"/api/v1/echo\"", RequestLines(sink).Single().Properties["RequestPath"].ToString());
    }
}

internal static class RequestExtensions
{
    public static Task<HttpResponseMessage> SendOnAsync(this HttpRequestMessage request, HttpClient client) => client.SendAsync(request);
}
