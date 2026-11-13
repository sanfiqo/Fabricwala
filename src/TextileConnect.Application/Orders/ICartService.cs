using TextileConnect.Domain.Orders;

namespace TextileConnect.Application.Orders;

public interface ICartService
{
    Task<IReadOnlyList<CartItem>> ListAsync(long businessId, CancellationToken cancellationToken = default);
    Task<CartItem> AddAsync(long businessId, string userId, long variantId, int quantity, CancellationToken cancellationToken = default);
    Task<CartItem?> UpdateAsync(long businessId, long itemId, int quantity, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(long businessId, long itemId, CancellationToken cancellationToken = default);
}
