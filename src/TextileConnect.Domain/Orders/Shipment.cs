namespace TextileConnect.Domain.Orders;

public sealed class Shipment
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public long? LogisticsBusinessId { get; set; }
    public string? CarrierName { get; set; }
    public string? LrNumber { get; set; }
    public string Status { get; set; } = ShipmentProgress.Dispatched;
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ProofOfDeliveryKey { get; set; }
}
