using TextileConnect.Domain.Businesses;

namespace TextileConnect.Api;

public sealed record AdminBusinessSummaryResponse(long Id, BusinessType BusinessType, string DisplayName,
    string LegalName, VerificationStatus VerificationStatus, DateTime CreatedAtUtc, string? RejectionNote, string Phone);
