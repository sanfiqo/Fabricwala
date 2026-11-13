using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Api;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/categories", () => Results.Ok(CategorySeed.Names));
        app.MapGet("/api/v1/products", (string? q, long? categoryId, decimal? minPrice, decimal? maxPrice, int? page, int? pageSize, IProductDirectory directory) =>
        {
            var currentPage = Math.Max(1, page ?? 1); var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var items = directory.ListPublic(q, categoryId, minPrice, maxPrice, currentPage, size, out var total);
            return Results.Ok(new { items, total, page = currentPage, pageSize = size });
        });
        app.MapGet("/api/v1/products/{slug}", (string slug, IProductDirectory directory) => directory.GetDetail(slug) is { } product ? Results.Ok(product) : Results.NotFound());
        var group = app.MapGroup("/api/v1/seller/products").RequireAuthorization("ApprovedBusiness");
        group.MapPost("", (Product product, ICurrentBusiness current, IProductDirectory directory) => Results.Ok(directory.Add(current.RequireBusinessId(), product)));
        group.MapGet("", (ICurrentBusiness current, IProductDirectory directory) => Results.Ok(directory.ListForSeller(current.RequireBusinessId())));
        group.MapGet("/{id:long}", (long id, ICurrentBusiness current, IProductDirectory directory) => directory.GetForSeller(current.RequireBusinessId(), id) is { } product ? Results.Ok(product) : Results.NotFound());
        group.MapDelete("/{id:long}", (long id, ICurrentBusiness current, IProductDirectory directory) => directory.Remove(current.RequireBusinessId(), id) ? Results.NoContent() : Results.NotFound());
    }
}
