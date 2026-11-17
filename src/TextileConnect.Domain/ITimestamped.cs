namespace TextileConnect.Domain;

/// <summary>Rows that record when they were created and last changed. MarketplaceDbContext fills both on save.</summary>
public interface ITimestamped
{
    DateTime CreatedAtUtc { get; set; }
    DateTime UpdatedAtUtc { get; set; }
}
