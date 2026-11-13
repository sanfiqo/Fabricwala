using TextileConnect.Application.Businesses;
using TextileConnect.Application.Orders;

namespace TextileConnect.Api;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/orders").RequireAuthorization("ApprovedBusiness");
        retailer.MapGet("", async (ICurrentBusiness current, IOrderService orders, CancellationToken ct) => Results.Ok(await orders.ListForRetailerAsync(current.RequireBusinessId(), ct)));
        var seller = app.MapGroup("/api/v1/seller/orders").RequireAuthorization("ApprovedBusiness");
        seller.MapGet("", async (ICurrentBusiness current, IOrderService orders, CancellationToken ct) => Results.Ok(await orders.ListForSellerAsync(current.RequireBusinessId(), ct)));
        seller.MapGet("/{id:long}", async (long id, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.GetForSellerAsync(current.RequireBusinessId(), id, ct) is { } order ? Results.Ok(order) : Results.NotFound());
        seller.MapPost("/{id:long}/status", async (long id, SellerStatus request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateSellerOrderAsync(current.RequireBusinessId(), id, request.Status, request.Reason, request.LrNumber, ct) is { } order ? Results.Ok(order) : Results.NotFound());
        seller.MapPost("/{id:long}/shipment", async (long id, ShipmentStatus request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateShipmentAsync(current.RequireBusinessId(), id, request.Status, ct) is { } shipment ? Results.Ok(shipment) : Results.NotFound());
    }

    public sealed record SellerStatus(string Status, string? Reason, string? LrNumber);
    public sealed record ShipmentStatus(string Status);
}
