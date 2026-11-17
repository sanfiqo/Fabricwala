using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class ConcurrencyHttpTests
{
    private const long RetailerA = 30;
    private const long RetailerB = 31;

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, string key, long addressId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(new { deliveryAddressId = addressId }) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    /// <summary>Stock is exactly one order (the minimum order quantity). Two shops both have it in the cart and check out at the same moment: one gets it, the other is told it is gone.</summary>
    [Fact]
    public async Task Two_checkouts_for_the_last_stock_only_one_winsAsync()
    {
        await using var fixture = await CheckoutHttpTests.StartAsync();
        int ordersBefore;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            ordersBefore = await db.Orders.CountAsync(); // the shared test data already contains a few orders
            (await db.ProductVariants.SingleAsync(x => x.Id == 20)).StockQty = 10; // product 5 has a minimum order of 10
            await db.SaveChangesAsync();
        }
        using var shopA = await fixture.ClientAsync("Retailer", "shop-a", RetailerA);
        using var shopB = await fixture.ClientAsync("Retailer", "shop-b", RetailerB);
        Assert.Equal(HttpStatusCode.OK, (await shopA.PostAsJsonAsync("/api/v1/cart/items", new { variantId = 20, quantity = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shopB.PostAsJsonAsync("/api/v1/cart/items", new { variantId = 20, quantity = 10 })).StatusCode);

        var results = await Task.WhenAll(CheckoutAsync(shopA, "last-unit-a", 100), CheckoutAsync(shopB, "last-unit-b", 101));

        Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.Created));
        var loser = results.Single(x => x.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode); // "insufficient_stock" when it read the stock late, a concurrency conflict when it read it at the same time
        using var check = fixture.Services.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(0, (await verify.ProductVariants.SingleAsync(x => x.Id == 20)).StockQty); // never negative, never sold twice
        // The in-memory store has no transactions, so the losing request can leave its order row behind there. A real database saves the order and the stock change together.
        if (MySqlStore.CurrentConnectionString is not null)
            Assert.Equal(ordersBefore + 1, await verify.Orders.CountAsync());
    }
}
