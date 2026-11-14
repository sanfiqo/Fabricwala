using System.Security.Claims;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Orders;

namespace TextileConnect.Api;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cart").RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Retailer"));
        group.MapGet("", async (ICurrentBusiness current, ICartService cart, CancellationToken ct) => Results.Ok(ApiResponseMappings.Cart(await cart.GetViewAsync(current.RequireBusinessId(), ct))));
        group.MapPost("/items", async (CartRequest request, ClaimsPrincipal principal, ICurrentBusiness current, ICartService cart, CancellationToken ct) =>
            await cart.AddItemAsync(current.RequireBusinessId(), principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, request.VariantId, request.Quantity, ct) is not null ? Results.Ok(ApiResponseMappings.Cart(await cart.GetViewAsync(current.RequireBusinessId(), ct))) : Results.BadRequest());
        group.MapPut("/items/{id:long}", async (long id, CartRequest request, ICurrentBusiness current, ICartService cart, CancellationToken ct) =>
            await cart.UpdateAsync(current.RequireBusinessId(), id, request.Quantity, ct) is not null ? Results.Ok(ApiResponseMappings.Cart(await cart.GetViewAsync(current.RequireBusinessId(), ct))) : Results.NotFound());
        group.MapDelete("/items/{id:long}", async (long id, ICurrentBusiness current, ICartService cart, CancellationToken ct) =>
            await cart.RemoveAsync(current.RequireBusinessId(), id, ct) ? Results.Ok(ApiResponseMappings.Cart(await cart.GetViewAsync(current.RequireBusinessId(), ct))) : Results.NotFound());
    }

    public sealed record CartRequest(long VariantId, int Quantity);
}
