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
    public decimal GstRate { get; set; }
    public int Moq { get; set; }
    public ProductUnit Unit { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsDeleted { get; set; }
}
