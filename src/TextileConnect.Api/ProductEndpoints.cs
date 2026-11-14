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
        app.MapCatalogueEndpoints();
        var group = app.MapGroup("/api/v1/seller/products").RequireAuthorization("ApprovedBusiness");
        group.MapPost("", (ProductInput input, ICurrentBusiness current, IProductDirectory directory) => Results.Ok(ApiResponseMappings.ProductDetail(directory.AddAggregate(current.RequireBusinessId(), input.ToProduct(), input.Variants, input.Slabs))));
        group.MapGet("", (ICurrentBusiness current, IProductDirectory directory) => Results.Ok(directory.ListForSeller(current.RequireBusinessId()).Select(ApiResponseMappings.Product).ToList()));
        group.MapGet("/{id:long}", (long id, ICurrentBusiness current, IProductDirectory directory) => directory.GetForSeller(current.RequireBusinessId(), id) is { } product ? Results.Ok(ApiResponseMappings.Product(product)) : Results.NotFound());
        group.MapDelete("/{id:long}", (long id, ICurrentBusiness current, IProductDirectory directory) => directory.Remove(current.RequireBusinessId(), id) ? Results.NoContent() : Results.NotFound());
    }

    public sealed class ProductInput
    {
        public long CategoryId { get; set; }
        public required string Title { get; set; }
        public required string Slug { get; set; }
        public string? Description { get; set; }
        public decimal GstRate { get; set; }
        public int Moq { get; set; }
        public ProductUnit Unit { get; set; }
        public ProductStatus Status { get; set; }
        public IReadOnlyList<ProductVariant> Variants { get; set; } = Array.Empty<ProductVariant>();
        public IReadOnlyList<PriceSlab> Slabs { get; set; } = Array.Empty<PriceSlab>();
        public Product ToProduct() => new() { CategoryId = CategoryId, Title = Title, Slug = Slug, Description = Description, GstRate = GstRate, Moq = Moq, Unit = Unit, Status = Status };
    }
}
