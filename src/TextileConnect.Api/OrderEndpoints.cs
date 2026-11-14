using System.Security.Claims;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/orders").RequireAuthorization("ApprovedBusiness");
        app.MapPost("/api/v1/checkout", async (CheckoutRequest request, HttpRequest http, ClaimsPrincipal principal, ICurrentBusiness current, ICheckoutService checkout, CancellationToken ct) =>
        {
            var result = await checkout.CheckoutAsync(current.RequireBusinessId(), principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                request.DeliveryAddressId, http.Headers["Idempotency-Key"].ToString(), ct);
            return result.Replayed ? Results.Ok(ApiResponseMappings.Order(result.Order)) : Results.Created($"/api/v1/orders/{result.Order.Id}", ApiResponseMappings.Order(result.Order));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Retailer"));
        retailer.RequireAuthorization(policy => policy.RequireRole("Retailer"));
        retailer.MapGet("", async (string? group, int? page, int? pageSize, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var current1 = page ?? 1; var size = pageSize ?? 20;
            if (!RetailerOrderReader.IsValidGroup(group) || current1 is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            return Results.Ok(await new RetailerOrderReader(db).ListAsync(current.RequireBusinessId(), group, current1, size, ct));
        });
        retailer.MapGet("/{id:long}", async (long id, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
            await new RetailerOrderReader(db).DetailAsync(current.RequireBusinessId(), id, ct) is { } order ? Results.Ok(order) : Results.NotFound());
        retailer.MapPost("/{id:long}/confirm-delivery", async (long id, ICurrentBusiness current, IOrderService orders, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (await orders.ConfirmDeliveryAsync(current.RequireBusinessId(), id, ct) is null) return Results.NotFound();
            return Results.Ok(await new RetailerOrderReader(db).DetailAsync(current.RequireBusinessId(), id, ct));
        });
        var seller = app.MapGroup("/api/v1/seller/orders").RequireAuthorization("ApprovedBusiness");
        seller.MapGet("", async (ICurrentBusiness current, IOrderService orders, CancellationToken ct) => Results.Ok((await orders.ListForSellerAsync(current.RequireBusinessId(), ct)).Select(ApiResponseMappings.SellerOrder).ToList()));
        seller.MapGet("/{id:long}", async (long id, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.GetForSellerAsync(current.RequireBusinessId(), id, ct) is { } order ? Results.Ok(ApiResponseMappings.SellerOrder(order)) : Results.NotFound());
        seller.MapPost("/{id:long}/accept", async (long id, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateSellerOrderAsync(current.RequireBusinessId(), id, "Accepted", null, null, ct) is { } order ? Results.Ok(ApiResponseMappings.SellerOrder(order)) : Results.NotFound());
        seller.MapPost("/{id:long}/reject", async (long id, SellerReject request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateSellerOrderAsync(current.RequireBusinessId(), id, "Rejected", request.Reason, null, ct) is { } order ? Results.Ok(ApiResponseMappings.SellerOrder(order)) : Results.NotFound());
        seller.MapPost("/{id:long}/dispatch", async (long id, SellerDispatch request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateSellerOrderAsync(current.RequireBusinessId(), id, "Dispatched", null, request.LrNumber, ct) is { } order ? Results.Ok(ApiResponseMappings.SellerOrder(order)) : Results.NotFound());
        seller.MapPost("/{id:long}/status", async (long id, SellerStatus request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateSellerOrderAsync(current.RequireBusinessId(), id, request.Status, request.Reason, request.LrNumber, ct) is { } order ? Results.Ok(ApiResponseMappings.SellerOrder(order)) : Results.NotFound());
        seller.MapPost("/{id:long}/shipment", async (long id, ShipmentStatus request, ICurrentBusiness current, IOrderService orders, CancellationToken ct) =>
            await orders.UpdateShipmentAsync(current.RequireBusinessId(), id, request.Status, ct) is { } shipment ? Results.Ok(ApiResponseMappings.Shipment(shipment)) : Results.NotFound());
    }

    public sealed record SellerStatus(string Status, string? Reason, string? LrNumber);
    public sealed record ShipmentStatus(string Status);
    public sealed record SellerReject(string Reason);
    public sealed record SellerDispatch(string LrNumber, string? CarrierName);
    public sealed record CheckoutRequest(long DeliveryAddressId);
}
