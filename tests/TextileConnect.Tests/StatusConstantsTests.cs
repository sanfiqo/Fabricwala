using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class StatusConstantsTests
{
    private static MarketplaceDbContext NewContext()
        => new(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void Status_texts_are_the_values_the_database_and_the_API_already_use()
    {
        // These strings are stored in MySQL and shown by the API and the client. Changing one is a breaking change.
        Assert.Equal(new[] { "Pending", "Accepted", "Rejected", "Dispatched", "Delivered", "Cancelled" }, SellerOrderStatus.All);
        Assert.Equal(new[] { "Pending", "Paid", "Failed", "Cancelled", "Refunded", "PartiallyRefunded" }, PaymentStatus.All);
        Assert.Equal(new[] { "Pending", "Released", "Cancelled" }, SettlementStatus.All);
        Assert.Equal(new[] { "Requested", "Processed", "Failed" }, RefundStatus.All);
    }

    [Fact]
    public void Dead_seller_orders_are_the_rejected_and_the_cancelled_ones()
    {
        Assert.All(SellerOrderStatus.All, status => Assert.Equal(status is "Rejected" or "Cancelled", SellerOrderStatus.IsDead(status)));
    }

    [Theory]
    [InlineData("seller order")]
    [InlineData("order history")]
    [InlineData("order payment")]
    [InlineData("payment")]
    [InlineData("settlement")]
    [InlineData("refund")]
    public async Task An_unknown_status_is_refused_when_saving(string kind)
    {
        await using var db = NewContext();
        switch (kind)
        {
            case "seller order": db.SellerOrders.Add(new SellerOrder { Status = "Shiped" }); break;
            case "order history": db.OrderStatusHistories.Add(new OrderStatusHistory { Status = "Shiped" }); break;
            case "order payment": db.Orders.Add(new Order { OrderNumber = "X", PaymentStatus = "paid" }); break;
            case "payment": db.Payments.Add(new Payment { GatewayOrderId = "order_x", Status = "Succeeded" }); break;
            case "settlement": db.Settlements.Add(new Settlement { Status = "Done" }); break;
            case "refund": db.Refunds.Add(new Refund { Reason = "x", Status = "Complete" }); break;
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains(kind, error.Message);
    }

    [Fact]
    public async Task Known_statuses_save_fine()
    {
        await using var db = NewContext();
        db.Orders.Add(new Order { OrderNumber = "OK", PaymentStatus = PaymentStatus.PartiallyRefunded });
        db.Settlements.Add(new Settlement { Status = SettlementStatus.Released });
        Assert.Equal(2, await db.SaveChangesAsync());
    }
}
