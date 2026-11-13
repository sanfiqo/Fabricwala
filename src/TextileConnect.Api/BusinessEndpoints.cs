using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/businesses").RequireAuthorization();
        group.MapPost("", (Business business, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.Create(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", business)));
        group.MapGet("/me", (ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.GetForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "") is { } business
                ? Results.Ok(business) : Results.NotFound());
        group.MapPut("/me", (BusinessUpdate request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.UpdateForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", b =>
            { b.DisplayName = request.DisplayName; b.Email = request.Email; }) is { } business
                ? Results.Ok(business) : Results.NotFound());
        group.MapPost("/me/addresses", (BusinessAddress address, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddAddress(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", address)));
        group.MapPost("/me/documents", async (IFormFile file, string docType, ClaimsPrincipal principal, IBusinessDirectory directory, IFileStorage storage, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var business = directory.GetForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (business is null || file.Length == 0) return Results.BadRequest();
            await using var stream = file.OpenReadStream();
            var key = await storage.SavePrivateAsync(stream, file.FileName, ct);
            if (!Enum.TryParse<BusinessDocumentType>(docType, true, out var type)) return Results.BadRequest();
            var document = new BusinessDocument { BusinessId = business.Id, DocumentType = type, FileKey = key };
            db.BusinessDocuments.Add(document); await db.SaveChangesAsync(ct);
            return Results.Ok(new { document.Id, document.BusinessId, document.DocumentType, document.Status, url = $"/api/v1/businesses/me/documents/{document.Id}" });
        });
        group.MapPost("/me/users", (StaffRequest request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddStaff(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", request.UserId)));

        var admin = app.MapGroup("/api/v1/admin/businesses").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("", (IBusinessDirectory directory) => Results.Ok(directory.ListPending()));
        admin.MapPost("/{id:long}/approve", (long id, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.SetVerification(id, VerificationStatus.Approved, null, principal.FindFirstValue(ClaimTypes.NameIdentifier)) is { } business
                ? Results.Ok(business) : Results.NotFound());
        admin.MapPost("/{id:long}/reject", (long id, Rejection request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.SetVerification(id, VerificationStatus.Rejected, request.Note, principal.FindFirstValue(ClaimTypes.NameIdentifier)) is { } business
                ? Results.Ok(business) : Results.NotFound());
    }

    public sealed record BusinessUpdate(string DisplayName, string? Email);
    public sealed record Rejection(string Note);
    public sealed record StaffRequest(string UserId);
}
