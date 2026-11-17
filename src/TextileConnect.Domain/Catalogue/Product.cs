namespace TextileConnect.Domain.Catalogue;

public enum ProductStatus { Draft, PendingReview, Active, Inactive }
public enum ProductUnit { Piece, Set, Metre, Kg }

public sealed class Product
{
    public long Id { get; set; }
    public long SellerBusinessId { get; set; }
    public long CategoryId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? Material { get; set; }
    public string? FabricSpec { get; set; }
    public string? Season { get; set; }
    public string? HsnCode { get; set; }
    public decimal WeightPerUnitGrams { get; set; }
    /// <summary>Pieces of this product in seller orders that were delivered. Raised when the retailer confirms delivery; cancelled, rejected and reported orders never raise it. Used to sort by popularity.</summary>
    public int UnitsSold { get; set; }
    public int DispatchDays { get; set; }
    public string? ReturnPolicy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal GstRate { get; set; }
    public int Moq { get; set; }
    public ProductUnit Unit { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsDeleted { get; set; }
    /// <summary>Set when an admin switches the product off; the seller cannot switch it back on.</summary>
    public bool AdminHold { get; set; }
}
