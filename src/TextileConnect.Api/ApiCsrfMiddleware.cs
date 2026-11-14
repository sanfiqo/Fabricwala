using Microsoft.AspNetCore.Antiforgery;

namespace TextileConnect.Api;

public sealed class ApiCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1")
            && !context.Request.Path.StartsWithSegments("/api/v1/webhooks")
            && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method) && !HttpMethods.IsTrace(context.Request.Method))
            await antiforgery.ValidateRequestAsync(context);
        await next(context);
    }
}
