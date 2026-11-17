using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Every from-to pair of seller order statuses, in the rule table and in the service that applies it.</summary>
public sealed class OrderStateMachineTests
{
    private static readonly IReadOnlyDictionary<string, string[]> Expected = new Dictionary<string, string[]>
    {
        ["Pending"] = ["Accepted", "Rejected", "Cancelled"],
        ["Accepted"] = ["Dispatched"],
        ["Dispatched"] = ["Delivered"],
        ["Rejected"] = [],
        ["Cancelled"] = [],
        ["Delivered"] = []
    };

    public static IEnumerable<object[]> AllPairs()
        => SellerOrderStatus.All.SelectMany(from => SellerOrderStatus.All.Select(to => new object[] { from, to }));

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void The_rule_table_allows_exactly_the_documented_moves(string from, string to)
        => Assert.Equal(Expected[from].Contains(to), SellerOrderTransitions.CanMove(from, to));

    [Fact]
    public void Unknown_statuses_cannot_move_anywhere()
    {
        Assert.False(SellerOrderTransitions.CanMove("Shiped", "Delivered"));
        Assert.False(SellerOrderTransitions.CanMove("Pending", "Shiped"));
        Assert.Empty(SellerOrderTransitions.From("Nonsense"));
    }

    private static async Task<(MarketplaceDbContext Db, EfOrderService Service, SellerOrder SellerOrder, Order Order)> SetUpAsync(string status)
    {
        var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var order = new Order { OrderNumber = "SM-" + status, RetailerBusinessId = 1, PaymentStatus = PaymentStatus.Paid };
        var sellerOrder = new SellerOrder { Order = order, ManufacturerBusinessId = 2, Status = status };
        db.SellerOrders.Add(sellerOrder);
        await db.SaveChangesAsync();
        return (db, new EfOrderService(db), sellerOrder, order);
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public async Task The_service_applies_the_rule_for_every_seller_action_and_status(string from, string to)
    {
        // The seller can ask for Accepted, Rejected or Dispatched. Delivered belongs to the retailer, Cancelled to the system, Pending is never a target.
        if (to is not ("Accepted" or "Rejected" or "Dispatched")) return;
        var (db, service, sellerOrder, _) = await SetUpAsync(from);
        await using var _db = db;

        var call = () => service.UpdateSellerOrderAsync(2, sellerOrder.Id, to, "Out of stock", "LR-1");
        if (SellerOrderTransitions.CanMove(from, to))
            Assert.Equal(to, (await call())!.Status);
        else
        {
            var error = await Assert.ThrowsAsync<RuleViolationException>(call);
            Assert.Equal("invalid_order_transition", error.Code);
            Assert.Equal(from, (await db.SellerOrders.AsNoTracking().SingleAsync()).Status); // nothing changed
        }
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Accepted")]
    [InlineData("Dispatched")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    [InlineData("Delivered")]
    public async Task The_retailer_can_confirm_delivery_only_of_a_shipped_order(string from)
    {
        var (db, service, sellerOrder, order) = await SetUpAsync(from);
        await using var _db = db;

        if (from == "Dispatched")
        {
            await service.ConfirmDeliveryAsync(1, order.Id);
            Assert.Equal("Delivered", (await db.SellerOrders.AsNoTracking().SingleAsync()).Status);
        }
        else if (from == "Delivered")
            Assert.NotNull(await service.ConfirmDeliveryAsync(1, order.Id)); // repeating it changes nothing
        else
            Assert.Equal("order_not_shipped", (await Assert.ThrowsAsync<RuleViolationException>(() => service.ConfirmDeliveryAsync(1, order.Id))).Code);
    }

    [Theory]
    [InlineData("InTransit", true)]
    [InlineData("ReachedCity", true)]
    [InlineData("Delivered", false)]
    [InlineData("Dispatched", false)]
    [InlineData("Teleported", false)]
    public async Task The_seller_can_only_report_shipment_progress_not_delivery(string progress, bool allowed)
    {
        var (db, service, sellerOrder, _) = await SetUpAsync("Dispatched");
        await using var _db = db;
        db.Shipments.Add(new Shipment { SellerOrderId = sellerOrder.Id, LrNumber = "LR-1" });
        await db.SaveChangesAsync();

        if (allowed) Assert.Equal(progress, (await service.UpdateShipmentAsync(2, sellerOrder.Id, progress))!.Status);
        else Assert.Equal("invalid_shipment_status", (await Assert.ThrowsAsync<RuleViolationException>(() => service.UpdateShipmentAsync(2, sellerOrder.Id, progress))).Code);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Accepted")]
    [InlineData("Delivered")]
    public async Task Shipment_progress_needs_a_shipped_order(string status)
    {
        var (db, service, sellerOrder, _) = await SetUpAsync(status);
        await using var _db = db;
        db.Shipments.Add(new Shipment { SellerOrderId = sellerOrder.Id });
        await db.SaveChangesAsync();
        Assert.Equal("invalid_order_transition", (await Assert.ThrowsAsync<RuleViolationException>(() => service.UpdateShipmentAsync(2, sellerOrder.Id, "InTransit"))).Code);
    }
}
