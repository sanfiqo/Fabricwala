using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Common;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed partial class EfCheckoutService(MarketplaceDbContext db, ICartService cart, IProductPricing pricing) : ICheckoutService
{
    private const string Scope = "checkout";
    private const decimal DefaultCommissionRate = 0.02m;

    [GeneratedRegex("^[A-Za-z0-9._:-]{8,100}$")]
    private static partial Regex KeyPattern();

    public async Task<CheckoutResult> CheckoutAsync(long retailerBusinessId, string userId, long deliveryAddressId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!KeyPattern().IsMatch(idempotencyKey ?? string.Empty)) throw new ArgumentException("Idempotency-Key must be 8 to 100 letters, digits or . _ : -.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Scope}:{deliveryAddressId}"))).ToLowerInvariant();
        var previous = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.BusinessId == retailerBusinessId && x.Scope == Scope && x.Key == idempotencyKey, cancellationToken);
        if (previous is not null)
        {
            if (previous.RequestHash != hash) throw new RuleViolationException("idempotency_key_reused", "This Idempotency-Key was already used for a different request.");
            var existing = await db.Orders.Include(x => x.SellerOrders).ThenInclude(x => x.Items)
                .SingleAsync(x => x.OrderNumber == previous.ResourceKey && x.RetailerBusinessId == retailerBusinessId, cancellationToken);
            return new CheckoutResult(existing, true);
        }

        var address = await db.BusinessAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deliveryAddressId && x.BusinessId == retailerBusinessId && x.AddressType == AddressType.Delivery, cancellationToken)
            ?? throw new KeyNotFoundException("Delivery address not found.");
        var view = await cart.GetViewAsync(retailerBusinessId, cancellationToken);
        if (view.Sellers.Count == 0) throw new RuleViolationException("cart_empty", "Your cart is empty.");
        if (!view.CanCheckout) throw new RuleViolationException("cart_not_ready", "Fix the items marked in your cart before checkout.");

        PriceDestination? destination;
        try { destination = await pricing.DestinationAsync(address.Pincode, address.CityId, retailerBusinessId, cancellationToken); }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        { throw new RuleViolationException("delivery_unavailable", "We cannot deliver to this address yet."); }
        if (destination is null) throw new RuleViolationException("delivery_unavailable", "We cannot deliver to this address yet.");

        var lines = view.Sellers.SelectMany(x => x.Items).ToList();
        var productQuantities = lines.GroupBy(x => x.ProductId).Select(x => new ProductQuantity(x.Key, x.First().ProductQuantity)).ToArray();
        var quotes = await pricing.QuoteAsync(productQuantities, destination, cancellationToken);
        if (quotes.Values.Any(x => x.ShippingTotal is null)) throw new RuleViolationException("shipping_unavailable", "Delivery to this address is not available for some items.");

        var variantIds = lines.Select(x => x.VariantId).ToArray();
        var variants = await db.ProductVariants.Where(x => variantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (lines.Any(x => !variants.TryGetValue(x.VariantId, out var v) || !v.IsActive || v.StockQty < x.Quantity))
            throw new RuleViolationException("insufficient_stock", "Some items are no longer available in the requested quantity.");
        var sellerIds = view.Sellers.Select(x => x.SellerBusinessId).ToArray();
        var sellers = await db.Businesses.AsNoTracking().Where(x => sellerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var defaultRate = await DefaultRateAsync(cancellationToken);
        var city = await db.Cities.AsNoTracking().Where(x => x.Id == address.CityId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        var now = DateTime.UtcNow;
        var order = new Order
        {
            OrderNumber = NewOrderNumber(now), RetailerBusinessId = retailerBusinessId, PlacedByUserId = userId, PlacedAtUtc = now,
            DeliveryLine1 = address.Line1, DeliveryLine2 = address.Line2, DeliveryCity = city, DeliveryPincode = address.Pincode,
        };
        foreach (var group in view.Sellers)
        {
            var rate = sellers[group.SellerBusinessId].CommissionRateOverride ?? defaultRate;
            var sellerOrder = new SellerOrder { Order = order, ManufacturerBusinessId = group.SellerBusinessId, CommissionRate = rate, PlacedAtUtc = now, Status = "Pending" };
            foreach (var line in group.Items)
            {
                sellerOrder.Items.Add(new OrderItem
                {
                    SellerOrder = sellerOrder, ProductId = line.ProductId, VariantId = line.VariantId, Title = line.Title, Size = line.Size, Colour = line.Colour,
                    Quantity = line.Quantity, UnitPrice = line.SlabPrice, GstRate = line.GstRate, GstAmount = line.LineGst,
                });
                variants[line.VariantId].StockQty -= line.Quantity;
            }
            sellerOrder.SubTotal = group.SubTotal;
            sellerOrder.TaxAmount = group.GstTotal;
            sellerOrder.ShippingAmount = group.Items.Select(x => x.ProductId).Distinct().Sum(id => quotes[id].ShippingTotal!.Value);
            sellerOrder.Total = sellerOrder.SubTotal + sellerOrder.TaxAmount + sellerOrder.ShippingAmount;
            sellerOrder.CommissionAmount = ProductQuote.Money(sellerOrder.SubTotal * rate);
            sellerOrder.History.Add(new OrderStatusHistory { Status = "Pending", Note = "Order placed; waiting for payment.", ChangedAtUtc = now });
            order.SellerOrders.Add(sellerOrder);
            order.SubTotal += sellerOrder.SubTotal;
            order.TaxTotal += sellerOrder.TaxAmount;
            order.ShippingTotal += sellerOrder.ShippingAmount;
        }
        order.GrandTotal = order.SubTotal + order.TaxTotal + order.ShippingTotal;
        db.Orders.Add(order);
        db.IdempotencyRecords.Add(new IdempotencyRecord { BusinessId = retailerBusinessId, Scope = Scope, Key = idempotencyKey, RequestHash = hash, ResourceKey = order.OrderNumber, CreatedAtUtc = now });
        db.CartItems.RemoveRange(await db.CartItems.Where(x => x.RetailerBusinessId == retailerBusinessId).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return new CheckoutResult(order, false);
    }

    private async Task<decimal> DefaultRateAsync(CancellationToken cancellationToken)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == "Commission.DefaultRate").Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) && rate is >= 0 and <= 1 ? rate : DefaultCommissionRate;
    }

    private static string NewOrderNumber(DateTime now)
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var suffix = new char[6];
        for (var i = 0; i < suffix.Length; i++) suffix[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"TC-{now:yyMMdd}-{new string(suffix)}";
    }
}
