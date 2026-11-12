using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Domain.Orders;

public static class CheckoutRules
{
    public static decimal Commission(decimal subtotal, decimal? overrideRate, PlatformSettings settings)
        => subtotal * (overrideRate ?? settings.CommissionDefaultRate);

    public static IReadOnlyDictionary<long, decimal> SplitBySeller(IEnumerable<(long SellerBusinessId, decimal Amount)> items)
        => items.GroupBy(x => x.SellerBusinessId).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
}
