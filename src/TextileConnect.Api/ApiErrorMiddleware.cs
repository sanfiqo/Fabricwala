using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;

namespace TextileConnect.Api;

public sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        var original = context.Response.Body;
        using var guarded = new ApiErrorBodyStream(original, context.Response);
        context.Response.Body = guarded;
        ApiErrorResponse? error = null;
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = exception switch
            {
                BadHttpRequestException badRequest => badRequest.StatusCode,
                ValidationException or ArgumentException or AntiforgeryValidationException => 400,
                UnauthorizedAccessException => 403,
                KeyNotFoundException => 404,
                TextileConnect.Application.Businesses.BusinessConflictException => 409,
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => 409,
                Microsoft.EntityFrameworkCore.DbUpdateException update
                    when update.InnerException is MySql.Data.MySqlClient.MySqlException { Number: 1062 } => 409,
                _ => 500
            };
            error = ApiErrorResponse.ForStatus(context.Response.StatusCode);
            if (exception is ValidationException validation)
                error = error with { Fields = validation.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(x => x.Key, _ => new[] { "invalid_value" }) };
            if (context.Response.StatusCode >= 500)
                logger.LogError("API failure {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        }
        finally
        {
            context.Response.Body = original;
        }

        if (context.Response.StatusCode >= 400 && !context.Response.HasStarted)
        {
            context.Response.ContentLength = null;
            context.Response.ContentType = "application/json";
            // HEAD has the same status and headers, without a response body.
            if (!HttpMethods.IsHead(context.Request.Method))
                await context.Response.WriteAsJsonAsync(error ?? ApiErrorResponse.ForStatus(context.Response.StatusCode), context.RequestAborted);
        }
    }
}
