namespace TextileConnect.Domain;

/// <summary>
/// Where the goods are. Dispatched is set when the seller ships, InTransit and ReachedCity are progress notes the seller may add,
/// Delivered is set only when the retailer confirms delivery (it also moves the seller order and the payout, so the seller cannot set it).
/// </summary>
public static class ShipmentProgress
{
    public const string Dispatched = "Dispatched";
    public const string InTransit = "InTransit";
    public const string ReachedCity = "ReachedCity";
    public const string Delivered = "Delivered";

    public static readonly IReadOnlyList<string> All = [Dispatched, InTransit, ReachedCity, Delivered];

    /// <summary>What the seller may report through the shipment route.</summary>
    public static readonly IReadOnlyList<string> SellerCanSet = [InTransit, ReachedCity];
}
