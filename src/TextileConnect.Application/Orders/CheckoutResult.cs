using TextileConnect.Domain.Orders;

namespace TextileConnect.Application.Orders;

/// <summary>Replayed is true when the same Idempotency-Key had already created this order.</summary>
public sealed record CheckoutResult(Order Order, bool Replayed);
