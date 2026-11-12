namespace TextileConnect.Domain.Catalogue;

public sealed class PlatformSettings
{
    public decimal CommissionDefaultRate { get; set; } = 0.02m;
    public decimal RetailerPlatformFeeRate { get; set; }
    public int SettlementAutoReleaseDays { get; set; } = 7;
    public string BrandName { get; set; } = "TextileConnect";
}
