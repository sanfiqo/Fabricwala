using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Api;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/categories", () => Results.Ok(CategorySeed.Names));
        app.MapGet("/api/v1/products", () => Results.Ok(Array.Empty<Product>()));
        app.MapGet("/api/v1/products/{slug}", (string slug) => Results.NotFound());
        var group = app.MapGroup("/api/v1/seller/products").RequireAuthorization("ApprovedBusiness");
        group.MapPost("", (Product product, long businessId, IProductDirectory directory) => Results.Ok(directory.Add(businessId, product)));
        group.MapGet("", (long businessId, IProductDirectory directory) => Results.Ok(directory.ListForSeller(businessId)));
        group.MapDelete("/{id:long}", (long id, long businessId, IProductDirectory directory) => directory.Remove(businessId, id) ? Results.NoContent() : Results.NotFound());
    }
}
