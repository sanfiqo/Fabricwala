namespace TextileConnect.Application.Catalogue;

public interface IProductPricing
{
    Task<PriceDestination?> DestinationAsync(string? pincode, long? cityId, long? retailerBusinessId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, ProductQuote>> QuoteAsync(IReadOnlyCollection<ProductQuantity> products, PriceDestination destination, CancellationToken ct = default);
}
