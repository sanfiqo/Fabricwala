namespace TextileConnect.Domain.Catalogue;

public sealed class ProductMedia
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public required string FileKey { get; set; }
    public required string MediaType { get; set; }
    public int SortOrder { get; set; }
}
