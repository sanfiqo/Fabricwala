using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Infrastructure.Orders;
using TextileConnect.Infrastructure.Persistence;
using Sanfiqo.Identity;

namespace TextileConnect.Tests;

public sealed class BusinessRulesExpandedTests
{
    [Theory]
    [InlineData("+919000000003", "+919000000003")]
    [InlineData("+91 90000 00003", "+919000000003")]
    [InlineData("+91-90000-00003", "+919000000003")]
    [InlineData("  +919000000003  ", "+919000000003")]
    [InlineData("+919876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("+919111111111", "+919111111111")]
    [InlineData("+919222222222", "+919222222222")]
    [InlineData("+919333333333", "+919333333333")]
    [InlineData("+919444444444", "+919444444444")]
    [InlineData("+919555555555", "+919555555555")]
    [InlineData("+919666666666", "+919666666666")]
    [InlineData("+919777777777", "+919777777777")]
    [InlineData("+919888888888", "+919888888888")]
    [InlineData("+919999999999", "+919999999999")]
    [InlineData("+918000000001", "+918000000001")]
    [InlineData("+918000000002", "+918000000002")]
    [InlineData("+918000000003", "+918000000003")]
    [InlineData("+918000000004", "+918000000004")]
    [InlineData("+918000000005", "+918000000005")]
    public void Bootstrap_phone_values_are_normalized(string configured, string expected)
    {
        Assert.True(UserContactNormalizer.TryNormalize(UserContactKind.Mobile, configured, out var contact));
        Assert.Equal(expected, contact.Value);
    }

    [Theory]
    [InlineData(10, 10, true)] [InlineData(10, 9, false)] [InlineData(50, 50, true)]
    [InlineData(50, 49, false)] [InlineData(100, 100, true)] [InlineData(100, 101, false)]
    public void Client_price_must_match_server(decimal client, decimal server, bool expected)
        => Assert.Equal(expected, CheckoutRules.IsClientPriceValid(client, server));

    [Theory]
    [InlineData(100, 0.02, 2)] [InlineData(100, 0.05, 5)] [InlineData(250, 0.02, 5)]
    [InlineData(0, 0.02, 0)] [InlineData(99.99, 0.01, 0.9999)]
    public void Commission_uses_override_or_default(decimal subtotal, decimal rate, decimal expected)
        => Assert.Equal(expected, CheckoutRules.Commission(subtotal, rate, new PlatformSettings()));

    [Theory]
    [InlineData(1, 0)] [InlineData(10, 10)] [InlineData(49, 10)] [InlineData(50, 50)]
    [InlineData(75, 50)] [InlineData(99, 50)] [InlineData(100, 100)] [InlineData(0, 0)]
    public void Slab_selection_is_deterministic(int quantity, int expectedMin)
    {
        var slabs = new[] { new PriceSlab { MinQty = 10, MaxQty = 49, PricePerUnit = 20 }, new PriceSlab { MinQty = 50, MaxQty = 99, PricePerUnit = 18 }, new PriceSlab { MinQty = 100, PricePerUnit = 16 } };
        Assert.Equal(expectedMin == 0 ? null : expectedMin, PriceSlabRules.Select(slabs, quantity)?.MinQty);
    }

    [Theory]
    [InlineData(1, 1, 5)] [InlineData(2, 1, 5)] [InlineData(5, 2, 10)]
    [InlineData(10, 2, 10)] [InlineData(20, 3, 20)] [InlineData(100, 3, 20)]
    public void Landed_cost_applies_per_piece_rate(decimal qty, decimal weight, decimal expectedFreight)
    {
        var result = LandedCostCalculator.Calculate(100, (int)qty, 0, weight, new ShippingRate { RateType = ShippingRateType.PerPiece, Rate = expectedFreight }, 0);
        Assert.Equal(expectedFreight, result.FreightPerUnit);
    }

    [Fact]
    public async Task Full_order_flow_supports_payment_accept_dispatch_delivery()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MarketplaceDbContext(options);
        db.Businesses.AddRange(new Business { Id = 1, BusinessType = BusinessType.Retailer, LegalName = "R", DisplayName = "R", Phone = "+91r", VerificationStatus = VerificationStatus.Approved }, new Business { Id = 2, BusinessType = BusinessType.Manufacturer, LegalName = "M", DisplayName = "M", Phone = "+91m", VerificationStatus = VerificationStatus.Approved });
        db.BusinessAddresses.AddRange(new BusinessAddress { Id = 1, BusinessId = 1, AddressType = AddressType.Delivery, Line1 = "A", CityId = 1, Pincode = "190001" },
            new BusinessAddress { Id = 2, BusinessId = 2, AddressType = AddressType.Pickup, Line1 = "Factory", CityId = 1, Pincode = "190001", IsDefault = true });
        db.Cities.Add(new City { Id = 1, StateId = 1, Name = "Srinagar" });
        db.PostalCodes.Add(new PostalCode { Pincode = "190001", CityId = 1 });
        db.ShippingRates.Add(new ShippingRate { FromCityId = 1, ToCityId = 1, RateType = ShippingRateType.PerPiece, Rate = 5, MinCharge = 0, TransitDays = 2 });
        db.Products.Add(new Product { Id = 1, SellerBusinessId = 2, CategoryId = 1, Title = "Coat", Slug = "coat", Moq = 10, Status = ProductStatus.Active });
        db.ProductVariants.Add(new ProductVariant { Id = 1, ProductId = 1, Sku = "COAT-1", StockQty = 20 });
        db.PriceSlabs.Add(new PriceSlab { ProductId = 1, MinQty = 10, PricePerUnit = 100 });
        db.CartItems.Add(new CartItem { Id = 1, RetailerBusinessId = 1, AddedByUserId = "retailer", VariantId = 1, Quantity = 10 });
        await db.SaveChangesAsync();
        var order = (await new EfCheckoutService(db, new EfCartService(db), new EfProductPricing(db)).CheckoutAsync(1, "retailer", 1, "journey-key-1")).Order;
        Assert.NotNull(order);
        order!.PaymentStatus = PaymentStatus.Paid; await db.SaveChangesAsync();
        var service = new EfOrderService(db);
        Assert.Equal("Accepted", (await service.UpdateSellerOrderAsync(2, order.SellerOrders.Single().Id, "Accepted", null, null))!.Status);
        Assert.Equal("Dispatched", (await service.UpdateSellerOrderAsync(2, order.SellerOrders.Single().Id, "Dispatched", null, "LR-1"))!.Status);
        Assert.Equal("ReachedCity", (await service.UpdateShipmentAsync(2, order.SellerOrders.Single().Id, "ReachedCity"))!.Status);
        await Assert.ThrowsAsync<TextileConnect.Application.Common.RuleViolationException>(() => service.UpdateShipmentAsync(2, order.SellerOrders.Single().Id, "Delivered")); // only the retailer confirms delivery
        var delivered = await service.ConfirmDeliveryAsync(1, order.Id);
        Assert.NotNull(delivered);
        Assert.Equal("Delivered", (await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal("Delivered", (await db.Shipments.SingleAsync()).Status);
    }
}
