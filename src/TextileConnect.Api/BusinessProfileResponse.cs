using TextileConnect.Domain.Businesses;

namespace TextileConnect.Api;

public sealed record BusinessProfileResponse(long Id, BusinessType BusinessType, string LegalName, string DisplayName,
    string? Gstin, string Phone, string? Email, VerificationStatus VerificationStatus, string? RejectionNote,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? Pincode, long? CityId, string? City, string? State);
