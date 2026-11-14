using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace TextileConnect.Application.Catalogue;

/// <summary>Validation for seller product requests. Field names are the JSON paths the client sent (for example tiers[0].pricePerUnit).</summary>
public static partial class SellerProductRules
{
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^[0-9]{4,8}$")]
    private static partial Regex HsnPattern();

    /// <summary>Throws ValidationException listing every broken rule. When publishing, the complete set of rules applies.</summary>
    public static void Validate(SellerProductRequest request, bool publishing, bool categoryExists)
    {
        var failures = new List<ValidationFailure>();
        void Fail(string field) => failures.Add(new ValidationFailure(field, "invalid_value"));

        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200) Fail("title");
        if (request.Description?.Length > 4000) Fail("description");
        if (request.Material?.Length > 100) Fail("material");
        if (request.FabricSpec?.Length > 200) Fail("fabricSpec");
        if (request.Season?.Length > 50) Fail("season");
        if (!string.IsNullOrWhiteSpace(request.HsnCode) && !HsnPattern().IsMatch(request.HsnCode.Trim())) Fail("hsnCode");
        if (request.ReturnPolicy?.Length > 1000) Fail("returnPolicy");
        if (request.WeightPerUnitGrams is < 0 or > 100_000) Fail("weightPerUnitGrams");
        if (request.DispatchDays is < 0 or > 90) Fail("dispatchDays");
        if (request.GstRate is < 0 or > 28) Fail("gstRate");
        if (request.Moq is < 0 or > 1_000_000) Fail("moq");
        if (!Enum.IsDefined(request.Unit)) Fail("unit");
        if (request.CategoryId is <= 0) Fail("categoryId");

        var variants = request.Variants ?? [];
        if (variants.Count > 200) Fail("variants");
        var seen = new HashSet<string>();
        for (var i = 0; i < Math.Min(variants.Count, 200); i++)
        {
            var v = variants[i];
            if (v.Size?.Length > 40) Fail($"variants[{i}].size");
            if (v.Colour?.Length > 40) Fail($"variants[{i}].colour");
            if (v.StockQty is < 0 or > 10_000_000) Fail($"variants[{i}].stockQty");
            if (!string.IsNullOrWhiteSpace(v.Sku) && !SkuPattern().IsMatch(v.Sku.Trim())) Fail($"variants[{i}].sku");
            if (!seen.Add($"{v.Size?.Trim().ToLowerInvariant()}|{v.Colour?.Trim().ToLowerInvariant()}")) Fail($"variants[{i}]");
        }

        var tiers = request.Tiers ?? [];
        if (tiers.Count > 10) Fail("tiers");
        for (var i = 0; i < Math.Min(tiers.Count, 10); i++)
        {
            if (tiers[i].MinQty is < 1 or > 1_000_000) Fail($"tiers[{i}].minQty");
            if (tiers[i].PricePerUnit is <= 0 or > 10_000_000 || decimal.Round(tiers[i].PricePerUnit, 2) != tiers[i].PricePerUnit) Fail($"tiers[{i}].pricePerUnit");
        }
        var ordered = tiers.Select((tier, index) => (tier, index)).OrderBy(x => x.tier.MinQty).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].tier.MinQty == ordered[i - 1].tier.MinQty) Fail($"tiers[{ordered[i].index}].minQty");
            else if (ordered[i].tier.PricePerUnit > ordered[i - 1].tier.PricePerUnit) Fail($"tiers[{ordered[i].index}].pricePerUnit"); // a bigger order never costs more per piece
        }

        if (publishing)
        {
            if (request.CategoryId is null || !categoryExists) Fail("categoryId");
            if (request.Moq < 1) Fail("moq");
            if (tiers.Count == 0) Fail("tiers");
            else if (ordered[0].tier.MinQty != request.Moq) Fail($"tiers[{ordered[0].index}].minQty"); // the first tier starts at the MOQ
            if (!variants.Any(x => x.IsActive)) Fail("variants");
        }
        if (failures.Count > 0) throw new ValidationException(failures);
    }
}
