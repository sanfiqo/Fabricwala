namespace TextileConnect.Application.Orders;

public interface ICheckoutService
{
    /// <summary>Creates one order (split per seller) from the cart. Throws RuleViolationException with a stable code when it cannot.</summary>
    Task<CheckoutResult> CheckoutAsync(long retailerBusinessId, string userId, long deliveryAddressId, string idempotencyKey, CancellationToken cancellationToken = default);
}
