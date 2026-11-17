namespace Sanfiqo.Identity;

public sealed record AuthProfileResponse(string UserId, string? Phone, string? FullName,
    IList<string> Roles, AuthBusinessProfile? Business, string PreferredLanguage, string? MfaPending = null);
