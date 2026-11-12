namespace TextileConnect.Domain.Catalogue;

public static class PriceSlabRules
{
    public static PriceSlab? Select(IEnumerable<PriceSlab> slabs, int quantity)
        => slabs.Where(x => x.MinQty <= quantity && (x.MaxQty is null || quantity <= x.MaxQty))
            .OrderByDescending(x => x.MinQty)
            .FirstOrDefault();

    public static IReadOnlyList<string> Validate(Product product, IEnumerable<PriceSlab> slabs)
    {
        var ordered = slabs.OrderBy(x => x.MinQty).ToList();
        var errors = new List<string>();
        if (ordered.Count == 0 || ordered[0].MinQty != product.Moq)
            errors.Add("The first price slab MinQty must equal MOQ.");
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i - 1].MaxQty is null || ordered[i - 1].MaxQty >= ordered[i].MinQty)
                errors.Add("Price slabs must not overlap.");
        return errors;
    }
}
