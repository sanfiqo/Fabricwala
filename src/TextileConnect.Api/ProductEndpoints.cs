using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Catalogue;

namespace TextileConnect.Api;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapCatalogueEndpoints();
        app.MapSellerProductMediaEndpoints();
        var group = app.MapGroup("/api/v1/seller/products").RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Manufacturer"));
        group.MapGet("", async (string? filter, string? q, int? threshold, int? page, int? pageSize, ICurrentBusiness current, ISellerProductService products, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20; var low = threshold ?? 5;
            if (filter is not (null or "all" or "low_stock" or "draft" or "active" or "inactive") || number is < 1 or > 1_000_000 || size is < 1 or > 100
                || low is < 0 or > 1000 || q?.Length > 100) return Results.BadRequest();
            return Results.Ok(await products.ListAsync(current.RequireBusinessId(), filter, q, low, number, size, ct));
        });
        group.MapGet("/{id:long}", async (long id, ICurrentBusiness current, ISellerProductService products, CancellationToken ct) =>
            await products.GetAsync(current.RequireBusinessId(), id, ct) is { } product ? Results.Ok(product) : Results.NotFound());
        group.MapPost("", async (SellerProductRequest request, ICurrentBusiness current, ISellerProductService products, CancellationToken ct) =>
        {
            var product = await products.CreateAsync(current.RequireBusinessId(), request, ct);
            return Results.Created($"/api/v1/seller/products/{product.Id}", product);
        });
        group.MapPut("/{id:long}", async (long id, SellerProductRequest request, ICurrentBusiness current, ISellerProductService products, CancellationToken ct) =>
            await products.UpdateAsync(current.RequireBusinessId(), id, request, ct) is { } product ? Results.Ok(product) : Results.NotFound());
        group.MapPut("/{id:long}/active", async (long id, ActiveRequest request, ICurrentBusiness current, ISellerProductService products, CancellationToken ct) =>
            await products.SetActiveAsync(current.RequireBusinessId(), id, request.Active, ct) is { } product ? Results.Ok(product) : Results.NotFound());
        group.MapDelete("/{id:long}", (long id, ICurrentBusiness current, IProductDirectory directory) => directory.Remove(current.RequireBusinessId(), id) ? Results.NoContent() : Results.NotFound());
    }

    /// <summary>Body of the on/off toggle.</summary>
    public sealed record ActiveRequest(bool Active);
}
