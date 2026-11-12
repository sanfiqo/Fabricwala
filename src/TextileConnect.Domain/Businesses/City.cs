namespace TextileConnect.Domain.Businesses;

public sealed class City
{
    public long Id { get; set; }
    public long StateId { get; set; }
    public required string Name { get; set; }
    public bool IsDeliveryZone { get; set; }
    public bool IsSourcingHub { get; set; }
}
