namespace Sanfiqo.Identity;

public static class BusinessClaimTypes
{
    public const string BusinessId = "business_id";
    public const string BusinessType = "business_type";
    public const string Approved = "business_approved";
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        BusinessId, BusinessType, Approved
    };
}
