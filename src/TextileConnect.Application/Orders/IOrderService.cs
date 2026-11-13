using TextileConnect.Domain.Orders;

namespace TextileConnect.Application.Orders;

public interface IOrderService
{
    Task<IReadOnlyList<Order>> ListForRetailerAsync(long businessId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerOrder>> ListForSellerAsync(long businessId, CancellationToken cancellationToken = default);
    Task<SellerOrder?> UpdateSellerOrderAsync(long businessId, long sellerOrderId, string status, string? reason, string? lrNumber, CancellationToken cancellationToken = default);
    Task<Shipment?> UpdateShipmentAsync(long businessId, long sellerOrderId, string status, CancellationToken cancellationToken = default);
}
