using TextileConnect.Application.Common;
using TextileConnect.Application.Features;

namespace TextileConnect.Api;

/// <summary>Answers 404 <c>feature_off</c> for every route of a group while the feature is switched off (see <see cref="Feature"/>). It runs after the sign-in and role checks.</summary>
public static class FeatureGate
{
    public static RouteGroupBuilder RequireFeature(this RouteGroupBuilder group, string feature)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlags>();
            if (!await flags.IsOnAsync(feature, context.HttpContext.RequestAborted))
                throw new RuleViolationException("feature_off", "This feature is not available yet.", StatusCodes.Status404NotFound);
            return await next(context);
        });
        return group;
    }
}

public sealed record FeatureSwitch(bool On);

public static class FeatureEndpoints
{
    public static void MapFeatureEndpoints(this WebApplication app)
    {
        // Which optional parts are on, so the client can hide the screens of the others. Nothing private in it.
        app.MapGet("/api/v1/features", async (IFeatureFlags flags, CancellationToken ct) => Results.Ok(await flags.AllAsync(ct)));

        var admin = app.MapGroup("/api/v1/admin/features").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapPut("/{name}", async (string name, FeatureSwitch request, IFeatureFlags flags, IAuditTrail audit, CancellationToken ct) =>
        {
            if (!Feature.All.Contains(name)) return Results.NotFound();
            await flags.SetAsync(name, request.On, ct);
            audit.Record("feature.switched", "PlatformSetting", name, request.On ? "on" : "off");
            return Results.Ok(await flags.AllAsync(ct));
        });
    }
}
