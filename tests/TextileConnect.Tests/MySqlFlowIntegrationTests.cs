using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Infrastructure.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class MySqlFlowIntegrationTests
{
    [MySqlFact]
    public Task MySql_product_cart_checkout_payment_and_delivery_flow()
        => MySqlStore.RunAsync(() => FlowAsync(MySqlStore.CurrentConnectionString!));

    private static async Task FlowAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseMySQL(connectionString)
            .Options;
        await using var db = new MarketplaceDbContext(options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var cityId = await db.Cities.Select(x => x.Id).FirstAsync();
        var categoryId = await db.Categories.Select(x => x.Id).FirstAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var retailer = new Business { BusinessType = BusinessType.Retailer, LegalName = $"Test Retailer {suffix}", DisplayName = "Test Retailer", Phone = $"+9199{suffix}"[..13], VerificationStatus = VerificationStatus.Approved };
        var manufacturer = new Business { BusinessType = BusinessType.Manufacturer, LegalName = $"Test Manufacturer {suffix}", DisplayName = "Test Manufacturer", Phone = $"+9188{suffix}"[..13], VerificationStatus = VerificationStatus.Approved };
        db.Businesses.AddRange(retailer, manufacturer);
        await db.SaveChangesAsync();
        var address = new BusinessAddress { BusinessId = retailer.Id, AddressType = AddressType.Delivery, Line1 = "Test address", CityId = cityId, Pincode = "190001", IsDefault = true };
        var product = new Product { SellerBusinessId = manufacturer.Id, CategoryId = categoryId, Title = "Integration product", Slug = $"integration-{suffix}", Moq = 10, Status = ProductStatus.Active, Unit = ProductUnit.Piece };
        db.BusinessAddresses.Add(address);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var variant = new ProductVariant { ProductId = product.Id, Sku = $"INT-{suffix}", StockQty = 100, IsActive = true };
        db.ProductVariants.Add(variant);
        db.PriceSlabs.Add(new PriceSlab { ProductId = product.Id, MinQty = 10, MaxQty = 100, PricePerUnit = 100 });
        await db.SaveChangesAsync();

        db.BusinessAddresses.Add(new BusinessAddress { BusinessId = manufacturer.Id, AddressType = AddressType.Pickup, Line1 = "Test factory", CityId = cityId, Pincode = "190001", IsDefault = true });
        if (!await db.PostalCodes.AnyAsync(x => x.Pincode == "190001")) db.PostalCodes.Add(new PostalCode { Pincode = "190001", CityId = cityId });
        if (!await db.ShippingRates.AnyAsync(x => x.FromCityId == cityId && x.ToCityId == cityId && x.RateType == ShippingRateType.PerPiece))
            db.ShippingRates.Add(new ShippingRate { FromCityId = cityId, ToCityId = cityId, RateType = ShippingRateType.PerPiece, Rate = 5, MinCharge = 0, TransitDays = 2 });
        await db.SaveChangesAsync();
        var cart = new EfCartService(db);
        var checkout = new EfCheckoutService(db, cart, new EfProductPricing(db));
        var orders = new EfOrderService(db);
        await cart.AddAsync(retailer.Id, "integration-retailer", variant.Id, 10);
        var order = (await checkout.CheckoutAsync(retailer.Id, "integration-retailer", address.Id, "integration-key-1")).Order;
        Assert.NotNull(order);
        order!.PaymentStatus = PaymentStatus.Paid;
        await db.SaveChangesAsync();
        var sellerOrder = order.SellerOrders.Single();
        Assert.Equal("Accepted", (await orders.UpdateSellerOrderAsync(manufacturer.Id, sellerOrder.Id, "Accepted", null, null))!.Status);
        Assert.Equal("Dispatched", (await orders.UpdateSellerOrderAsync(manufacturer.Id, sellerOrder.Id, "Dispatched", null, "LR-INT"))!.Status);
        Assert.Equal("InTransit", (await orders.UpdateShipmentAsync(manufacturer.Id, sellerOrder.Id, "InTransit"))!.Status);   // the seller may note progress; only the retailer confirms delivery
        Assert.Equal("Delivered", (await orders.ConfirmDeliveryAsync(retailer.Id, order.Id))!.SellerOrders.Single().Status);

        await transaction.RollbackAsync();
    }
}
