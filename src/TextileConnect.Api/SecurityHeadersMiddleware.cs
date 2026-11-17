namespace TextileConnect.Api;

/// <summary>
/// Headers every API answer carries. The API only returns JSON and files, so it can forbid everything else:
/// no page, script or frame may be built from its answers (Content-Security-Policy), it must not be put in a frame, and browsers must not guess content types.
/// Private answers (a signed-in caller, or anything that changes data) are marked not to be stored by browsers or shared caches.
/// Swagger (Development only) is left out of the Content-Security-Policy because its page needs scripts.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
                var isPrivate = !HttpMethods.IsGet(context.Request.Method) || context.User.Identity?.IsAuthenticated == true;
                if (isPrivate) headers.TryAdd("Cache-Control", "no-store");
            }
            return Task.CompletedTask;
        });
        return next(context);
    }
}
