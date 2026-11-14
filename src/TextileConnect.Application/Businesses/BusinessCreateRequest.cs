using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public sealed record BusinessCreateRequest(BusinessType BusinessType, string LegalName, string DisplayName,
    string? Phone = null, string? Email = null, string? Gstin = null, string? Pincode = null)
{
    public Business ToBusiness(string? accountPhone) => new()
    {
        BusinessType = BusinessType, LegalName = LegalName, DisplayName = DisplayName,
        Phone = Phone ?? accountPhone ?? string.Empty, Email = Email, Gstin = Gstin
    };
}
