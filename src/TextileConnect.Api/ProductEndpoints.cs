using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Api;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/seller/products").RequireAuthorization("ApprovedBusiness");
        group.MapPost("", (Product product, IProductDirectory directory) => Results.Ok(directory.Add("current", product)));
        group.MapGet("", (long businessId, IProductDirectory directory) => Results.Ok(directory.ListForSeller(businessId)));
        group.MapDelete("/{id:long}", (long id, long businessId, IProductDirectory directory) => directory.Remove(businessId, id) ? Results.NoContent() : Results.NotFound());
    }
}
