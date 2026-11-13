using TextileConnect.Domain.Orders;

namespace TextileConnect.Application.Orders;

public interface ICheckoutService
{
    Task<Order?> CheckoutAsync(long retailerBusinessId, string userId, long deliveryAddressId, CancellationToken cancellationToken = default);
}
