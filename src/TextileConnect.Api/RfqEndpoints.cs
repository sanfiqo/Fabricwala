using TextileConnect.Application.Businesses;
using TextileConnect.Application.Features;
using TextileConnect.Application.Rfq;

namespace TextileConnect.Api;

public sealed record RfqCloseRequest(string? Reason);

/// <summary>
/// Requests for quotation (docs/RFQ.md): a retailer asks for a price, manufacturers quote, the retailer accepts one. All of it is behind the feature switch "Rfq".
/// Retailer: /rfqs. Manufacturer: /seller/rfqs and /seller/quotations. Admin: /admin/rfqs.
/// </summary>
public static class RfqEndpoints
{
    private static bool ValidPaging(int page, int size) => page is >= 1 and <= 1_000_000 && size is >= 1 and <= 100;

    public static void MapRfqEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/rfqs").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Retailer")).RequireFeature(Feature.Rfq);
        retailer.MapPost("", async (RfqInput request, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => Results.Ok(await rfqs.CreateAsync(current.RequireBusinessId(), request, ct)));
        retailer.MapGet("", async (string? status, int? page, int? pageSize, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct) =>
            ValidPaging(page ?? 1, pageSize ?? 20) ? Results.Ok(await rfqs.ListMineAsync(current.RequireBusinessId(), status, page ?? 1, pageSize ?? 20, ct)) : Results.BadRequest());
        retailer.MapGet("/{id:long}", async (long id, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.GetMineAsync(current.RequireBusinessId(), id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        retailer.MapPost("/{id:long}/cancel", async (long id, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.CancelAsync(current.RequireBusinessId(), id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        retailer.MapPost("/{id:long}/quotations/{quotationId:long}/accept", async (long id, long quotationId, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.AcceptAsync(current.RequireBusinessId(), id, quotationId, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());

        var seller = app.MapGroup("/api/v1/seller").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Manufacturer")).RequireFeature(Feature.Rfq);
        seller.MapGet("/rfqs", async (long? categoryId, string? q, int? page, int? pageSize, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct) =>
            ValidPaging(page ?? 1, pageSize ?? 20) && q?.Length is not > 100 ? Results.Ok(await rfqs.FeedAsync(current.RequireBusinessId(), categoryId, q, page ?? 1, pageSize ?? 20, ct)) : Results.BadRequest());
        seller.MapGet("/rfqs/{id:long}", async (long id, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.GetForSellerAsync(current.RequireBusinessId(), id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        seller.MapPut("/rfqs/{id:long}/quotation", async (long id, QuotationInput request, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.QuoteAsync(current.RequireBusinessId(), id, request, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        seller.MapDelete("/rfqs/{id:long}/quotation", async (long id, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct)
            => await rfqs.WithdrawAsync(current.RequireBusinessId(), id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        seller.MapGet("/quotations", async (string? status, int? page, int? pageSize, ICurrentBusiness current, IRfqService rfqs, CancellationToken ct) =>
            ValidPaging(page ?? 1, pageSize ?? 20) ? Results.Ok(await rfqs.MyQuotationsAsync(current.RequireBusinessId(), status, page ?? 1, pageSize ?? 20, ct)) : Results.BadRequest());

        var admin = app.MapGroup("/api/v1/admin/rfqs").RequireAuthorization(p => p.RequireRole("Admin")).RequireFeature(Feature.Rfq);
        admin.MapGet("", async (string? status, int? page, int? pageSize, IRfqService rfqs, CancellationToken ct) =>
            ValidPaging(page ?? 1, pageSize ?? 20) ? Results.Ok(await rfqs.ListAllAsync(status, page ?? 1, pageSize ?? 20, ct)) : Results.BadRequest());
        admin.MapPost("/{id:long}/close", async (long id, RfqCloseRequest request, IRfqService rfqs, CancellationToken ct)
            => await rfqs.CloseAsync(id, request.Reason, ct) ? Results.NoContent() : Results.NotFound());
    }
}
