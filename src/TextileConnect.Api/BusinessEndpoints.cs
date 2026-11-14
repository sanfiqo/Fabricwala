using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Sanfiqo.Identity;

namespace TextileConnect.Api;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/businesses").RequireAuthorization();
        group.MapPost("", async (BusinessCreateRequest request, ClaimsPrincipal principal, IBusinessDirectory directory, UserManager<ApplicationUser> users, MarketplaceDbContext db) =>
        {
            var user = await users.GetUserAsync(principal);
            var business = directory.Create(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", request.ToBusiness(user?.PhoneNumber), request.Pincode);
            return Results.Created("/api/v1/businesses/me", Profile(business, db));
        }).RequireAuthorization(policy => policy.RequireRole("Retailer", "Manufacturer"));
        group.MapGet("/me", (ClaimsPrincipal principal, IBusinessDirectory directory, MarketplaceDbContext db) =>
            directory.GetForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "") is { } business
                ? Results.Ok(Profile(business, db)) : Results.NotFound());
        group.MapPut("/me", (BusinessUpdate request, ClaimsPrincipal principal, IBusinessDirectory directory, MarketplaceDbContext db) =>
            directory.UpdateForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", b =>
            { b.DisplayName = request.DisplayName; b.Email = request.Email; }) is { } business
                ? Results.Ok(Profile(business, db)) : Results.NotFound());
        group.MapPost("/me/addresses", (BusinessAddress address, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddAddress(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", address)));
        group.MapGet("/me/addresses", (ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.ListAddresses(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "")));
        group.MapPut("/me/addresses/{id:long}/default", (long id, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.SetDefaultAddress(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", id) is { } address
                ? Results.Ok(address) : Results.NotFound());
        group.MapPost("/me/documents", async (IFormFile file, string docType, ClaimsPrincipal principal, IBusinessDirectory directory, IFileStorage storage, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var business = directory.GetForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (business is null || file.Length == 0) return Results.BadRequest();
            if (!Enum.TryParse<BusinessDocumentType>(docType, true, out var type)) return Results.BadRequest();
            await using var stream = file.OpenReadStream();
            var key = await storage.SavePrivateAsync(stream, file.FileName, ct);
            var document = new BusinessDocument { BusinessId = business.Id, DocumentType = type, FileKey = key };
            db.BusinessDocuments.Add(document); await db.SaveChangesAsync(ct);
            return Results.Ok(new { document.Id, document.BusinessId, document.DocumentType, document.Status, url = $"/api/v1/businesses/me/documents/{document.Id}" });
        });
        group.MapGet("/me/documents/{id:long}", async (long id, ClaimsPrincipal principal, ICurrentBusiness current, IFileStorage storage, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var document = await db.BusinessDocuments.SingleOrDefaultAsync(x => x.Id == id && x.BusinessId == current.RequireBusinessId(), ct);
            if (document is null) return Results.NotFound();
            var stream = await storage.OpenPrivateAsync(document.FileKey, ct);
            return Results.File(stream, "application/octet-stream", Path.GetFileName(document.FileKey));
        });
        group.MapPost("/me/users", (StaffRequest request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddStaff(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", request.UserId)));

        app.MapBusinessApprovalEndpoints();
    }

    internal static BusinessProfileResponse Profile(Business business, MarketplaceDbContext db)
    {
        var address = (from item in db.BusinessAddresses.AsNoTracking()
                       join city in db.Cities on item.CityId equals city.Id
                       join state in db.States on city.StateId equals state.Id
                       where item.BusinessId == business.Id && item.AddressType == AddressType.Registered
                       orderby item.Id
                       select new { item.Pincode, CityId = city.Id, City = city.Name, State = state.Name }).FirstOrDefault();
        return new(business.Id, business.BusinessType, business.LegalName, business.DisplayName,
            business.Gstin, business.Phone, business.Email, business.VerificationStatus, business.RejectionNote,
            business.CreatedAtUtc, business.UpdatedAtUtc, address?.Pincode, address?.CityId, address?.City, address?.State);
    }

    public sealed record BusinessUpdate(string DisplayName, string? Email);
    public sealed record Rejection(string Note);
    public sealed record StaffRequest(string UserId);
}
