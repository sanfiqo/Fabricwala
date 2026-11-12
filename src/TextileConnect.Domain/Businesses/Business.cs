namespace TextileConnect.Domain.Businesses;

public enum BusinessType { Retailer, Manufacturer, Logistics }
public enum VerificationStatus { Pending, Approved, Rejected, Suspended }

public sealed class Business
{
    public long Id { get; set; }
    public BusinessType BusinessType { get; set; }
    public required string LegalName { get; set; }
    public required string DisplayName { get; set; }
    public string? Gstin { get; set; }
    public string? Pan { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public string? RejectionNote { get; set; }
    public bool IsPlatformOwned { get; set; }
    public decimal? CommissionRateOverride { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
