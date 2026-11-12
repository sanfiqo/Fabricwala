namespace TextileConnect.Domain.Catalogue;

public sealed class Category
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
