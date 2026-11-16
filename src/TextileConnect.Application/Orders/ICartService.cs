using TextileConnect.Domain.Orders;

namespace TextileConnect.Application.Orders;

public interface ICartService
{
    Task<IReadOnlyList<CartItem>> ListAsync(long businessId, CancellationToken cancellationToken = default);
    Task<CartView> GetViewAsync(long businessId, CancellationToken cancellationToken = default);
    /// <summary>Storage-level add without catalogue checks. Endpoints use <see cref="AddItemAsync"/>.</summary>
    Task<CartItem> AddAsync(long businessId, string userId, long variantId, int quantity, CancellationToken cancellationToken = default);
    /// <summary>Validated add: variant/product must be purchasable, quantity 1..1,000,000 and within stock. Throws ArgumentException or KeyNotFoundException.</summary>
    Task<CartItem> AddItemAsync(long businessId, string userId, long variantId, int quantity, CancellationToken cancellationToken = default);
    Task<CartItem?> UpdateAsync(long businessId, long itemId, int quantity, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(long businessId, long itemId, CancellationToken cancellationToken = default);
}
