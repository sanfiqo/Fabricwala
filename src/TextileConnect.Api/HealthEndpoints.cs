using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Can the app reach its database? A short timeout, so a dead database answers "not ready" quickly instead of hanging the probe.</summary>
public sealed class DatabaseHealthCheck(MarketplaceDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { return await db.Database.CanConnectAsync(timeout.Token) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("The database does not answer."); }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return HealthCheckResult.Unhealthy("The database does not answer."); } // no exception text: it can hold the server name
    }
}

/// <summary>Can the app read and write its file storage?</summary>
public sealed class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { return await storage.IsHealthyAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("The file storage does not answer."); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return HealthCheckResult.Unhealthy("The file storage does not answer."); }
    }
}

public static class HealthEndpoints
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);
        return services;
    }

    /// <summary>
    /// /api/v1/health and /api/v1/health/live: the process is running (always 200 while it can answer). Restart it if this fails.
    /// /api/v1/health/ready: the database and the file storage work (200, or 503 naming what is down). Send traffic only while this is 200.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/api/v1/health", new() { Predicate = _ => false });
        app.MapHealthChecks("/api/v1/health/live", new() { Predicate = _ => false });
        app.MapHealthChecks("/api/v1/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(x => x.Key, x => x.Value.Status.ToString())
                }));
            }
        });
    }
}
