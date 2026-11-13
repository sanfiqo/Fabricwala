using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Orders;
using TextileConnect.Infrastructure.Persistence;
using TextileConnect.Domain.Orders;

namespace TextileConnect.Tests;

public sealed class CheckoutTests
{
    [Fact]
    public async Task Orders_and_seller_orders_are_isolated_by_business()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MarketplaceDbContext(options);
        db.Orders.AddRange(new Order { OrderNumber = "O-1", RetailerBusinessId = 10, PlacedByUserId = "u", GrandTotal = 10 }, new Order { OrderNumber = "O-2", RetailerBusinessId = 20, PlacedByUserId = "v", GrandTotal = 20 });
        db.SellerOrders.AddRange(new SellerOrder { OrderId = 1, ManufacturerBusinessId = 30, SubTotal = 10 }, new SellerOrder { OrderId = 2, ManufacturerBusinessId = 40, SubTotal = 20 });
        await db.SaveChangesAsync();
        var service = new TextileConnect.Infrastructure.Orders.EfOrderService(db);

        Assert.Single(await service.ListForRetailerAsync(10));
        Assert.Single(await service.ListForSellerAsync(30));
        Assert.Null(await service.UpdateSellerOrderAsync(40, 1, "Rejected", "no", null));
    }

    [Fact]
    public async Task Cart_isolation_prevents_read_update_and_remove_across_businesses()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MarketplaceDbContext(options);
        var cart = new EfCartService(db);
        var item = await cart.AddAsync(10, "user-a", 42, 2);

        Assert.Empty(await cart.ListAsync(11));
        Assert.Null(await cart.UpdateAsync(11, item.Id, 9));
        Assert.False(await cart.RemoveAsync(11, item.Id));
        Assert.Equal(2, (await cart.ListAsync(10)).Single().Quantity);
    }

    [Fact]
    public void Checkout_splits_sellers_and_uses_two_percent_manufacturer_commission()
    {
        var split = CheckoutRules.SplitBySeller([(1, 100m), (2, 50m), (1, 25m)]);
        Assert.Equal(125m, split[1]);
        Assert.Equal(2m, CheckoutRules.Commission(100m, null, new PlatformSettings()));
    }

    [Fact]
    public void Checkout_rejects_a_tampered_client_price()
    {
        Assert.False(CheckoutRules.IsClientPriceValid(1m, 2m));
        Assert.True(CheckoutRules.IsClientPriceValid(2m, 2m));
    }
}
