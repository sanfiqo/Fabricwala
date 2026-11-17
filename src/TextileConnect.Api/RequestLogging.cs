using System.Security.Claims;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace TextileConnect.Api;

/// <summary>
/// Gives every request an id that is the same in its log lines, in the error log and in the X-Correlation-Id response header, so a customer who says
/// "it failed" can be matched to the exact log lines. A caller may send its own id (8 to 64 letters, digits, dot, dash, underscore); anything else is replaced.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    [GeneratedRegex("^[A-Za-z0-9._-]{8,64}$")]
    private static partial Regex Valid();

    public async Task InvokeAsync(HttpContext context)
    {
        var sent = context.Request.Headers[Header].ToString();
        var id = Valid().IsMatch(sent) ? sent : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id; // the error middleware logs this as the trace id
        context.Response.OnStarting(() => { context.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (LogContext.PushProperty("CorrelationId", id)) await next(context);
    }
}

public static class RequestLogging
{
    /// <summary>
    /// Settings that must hold whatever the configuration says, so they are set in code after the configuration is read.
    /// The framework's own request lines ("Request starting ... {Path}{QueryString}") and the database command lines would write query strings (one-time codes, phone numbers)
    /// and SQL into the log, so those sources only speak when something is wrong. Request logging below replaces them.
    /// </summary>
    public static LoggerConfiguration KeepSecretsOut(this LoggerConfiguration configuration) => configuration
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);

    /// <summary>
    /// One log line per request: method, path (without the query string), status, time, signed-in user id and client address. Nothing else:
    /// no query string, no headers, no body, so one-time codes, mobile numbers, tokens and documents never reach the logs. Health probes are logged only when they fail.
    /// </summary>
    public static WebApplication UseApiRequestLogging(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "{RequestMethod:l} {RequestPath:l} answered {StatusCode} in {Elapsed:0} ms";
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/api/v1/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                diagnostics.Set("UserId", context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
                diagnostics.Set("ClientIp", ApiRateLimiting.ClientKey(context));
            };
        });
        return app;
    }
}
