namespace TextileConnect.Domain.Businesses;

public enum AddressType { Registered, Pickup, Delivery }

public sealed class BusinessAddress
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public AddressType AddressType { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public long CityId { get; set; }
    public required string Pincode { get; set; }
    public bool IsDefault { get; set; }
}
