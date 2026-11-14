using System.Text.Json.Serialization;

namespace TextileConnect.Api;

public sealed record ApiErrorResponse(string Code, string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static ApiErrorResponse ForStatus(int status) => status switch
    {
        400 => new("validation_failed", "Check the information and try again."),
        401 => new("authentication_required", "Sign in to continue."),
        403 => new("forbidden", "You do not have access to this action."),
        404 => new("not_found", "The requested item was not found."),
        405 => new("method_not_allowed", "This action is not available."),
        409 => new("conflict", "The item changed. Refresh and try again."),
        413 => new("payload_too_large", "The uploaded file is too large."),
        415 => new("unsupported_media_type", "This file or request format is not supported."),
        422 => new("validation_failed", "Check the information and try again."),
        429 => new("rate_limited", "Please wait before trying again."),
        503 => new("service_unavailable", "The service is temporarily unavailable."),
        >= 500 => new("internal_error", "Something went wrong. Please try again."),
        _ => new("request_failed", "The request could not be completed.")
    };
}
