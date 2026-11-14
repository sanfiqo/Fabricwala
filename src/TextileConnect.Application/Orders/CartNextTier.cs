namespace TextileConnect.Application.Orders;

/// <summary>Next cheaper price tier for a product: buy AddQuantity more to unlock UnitPrice.</summary>
public sealed record CartNextTier(int MinQuantity, decimal UnitPrice, int AddQuantity, decimal SavingPerUnit, decimal SavingTotal);
