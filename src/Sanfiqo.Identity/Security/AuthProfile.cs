namespace Sanfiqo.Identity;

public sealed record AuthBusinessProfile(long Id, string Type, string DisplayName, string VerificationStatus, string? RejectionNote);
public interface IAuthBusinessProfileReader
{
    Task<AuthBusinessProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default);
}
