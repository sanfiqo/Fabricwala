namespace TextileConnect.Domain.Businesses;

public sealed class DeliveryZone
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string PincodePrefix { get; set; }
    public bool IsActive { get; set; } = true;
}
