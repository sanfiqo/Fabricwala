using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sanfiqo.Identity;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed record AccountResponse(string UserId, string? Phone, string? FullName, string Language, IReadOnlyList<string> Roles, BusinessProfileResponse? Business);

/// <summary>Type is Delivery (retailers) or Pickup (manufacturers).</summary>
public sealed record AddressResponse(long Id, string Type, string Line1, string? Line2, string Pincode, long CityId, string City, string State, bool IsDefault);

public sealed record AddressRequest(string? Line1, string? Line2, string? Pincode);

public sealed record AccountUpdateRequest(string? FullName, string? Language);

public static class AccountEndpoints
{
    public const int MaxAddresses = 20;
    private static readonly string[] Languages = ["en", "hi"];

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/account").RequireAuthorization();
        group.MapGet("", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, MarketplaceDbContext db, ICurrentBusiness current, CancellationToken ct) =>
            await users.GetUserAsync(principal) is { } user ? Results.Ok(await ProfileAsync(user, users, db, current, ct)) : Results.NotFound());
        group.MapPut("", async (AccountUpdateRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, MarketplaceDbContext db, ICurrentBusiness current, CancellationToken ct) =>
        {
            if (await users.GetUserAsync(principal) is not { } user) return Results.NotFound();
            var name = request.FullName?.Trim();
            if (name is { Length: > 100 } || (request.Language is not null && !Languages.Contains(request.Language)) || (request.FullName is null && request.Language is null)) return Results.BadRequest();
            if (request.FullName is not null) user.FullName = string.IsNullOrEmpty(name) ? null : name;
            if (request.Language is not null) user.PreferredLanguage = request.Language;
            if (!(await users.UpdateAsync(user)).Succeeded) return Results.BadRequest();
            return Results.Ok(await ProfileAsync(user, users, db, current, ct));
        });
        group.MapPut("/language", async (AccountUpdateRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (request.Language is null || !Languages.Contains(request.Language)) return Results.BadRequest();
            if (await users.GetUserAsync(principal) is not { } user) return Results.NotFound();
            user.PreferredLanguage = request.Language;
            return (await users.UpdateAsync(user)).Succeeded ? Results.Ok(new { language = user.PreferredLanguage }) : Results.BadRequest();
        });

        var addresses = group.MapGroup("/addresses").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Retailer", "Manufacturer"));
        addresses.MapGet("", async (ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
            Results.Ok(await ListAsync(db, current.RequireBusinessId(), TypeOf(principal), ct)));
        addresses.MapPost("", async (AddressRequest request, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var type = TypeOf(principal); var businessId = current.RequireBusinessId();
            var city = await ValidateAsync(request, db, ct);
            if (city is null) return Results.BadRequest();
            var existing = await db.BusinessAddresses.Where(x => x.BusinessId == businessId && x.AddressType == type).ToListAsync(ct);
            if (existing.Count >= MaxAddresses) throw new RuleViolationException("address_limit", "You can save up to 20 addresses.");
            var address = new BusinessAddress { BusinessId = businessId, AddressType = type, Line1 = request.Line1!.Trim(), Line2 = Clean(request.Line2), Pincode = request.Pincode!, CityId = city.Value, IsDefault = existing.Count == 0 };
            db.BusinessAddresses.Add(address);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/account/addresses/{address.Id}", (await ListAsync(db, businessId, type, ct)).Single(x => x.Id == address.Id));
        });
        addresses.MapPut("/{id:long}", async (long id, AddressRequest request, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var type = TypeOf(principal); var businessId = current.RequireBusinessId();
            var address = await db.BusinessAddresses.SingleOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && x.AddressType == type, ct);
            if (address is null) return Results.NotFound();
            var city = await ValidateAsync(request, db, ct);
            if (city is null) return Results.BadRequest();
            address.Line1 = request.Line1!.Trim(); address.Line2 = Clean(request.Line2); address.Pincode = request.Pincode!; address.CityId = city.Value;
            await db.SaveChangesAsync(ct);
            return Results.Ok((await ListAsync(db, businessId, type, ct)).Single(x => x.Id == id));
        });
        addresses.MapPut("/{id:long}/default", async (long id, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var type = TypeOf(principal); var businessId = current.RequireBusinessId();
            var all = await db.BusinessAddresses.Where(x => x.BusinessId == businessId && x.AddressType == type).ToListAsync(ct);
            if (all.All(x => x.Id != id)) return Results.NotFound();
            foreach (var item in all) item.IsDefault = item.Id == id;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ListAsync(db, businessId, type, ct));
        });
        addresses.MapDelete("/{id:long}", async (long id, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var type = TypeOf(principal); var businessId = current.RequireBusinessId();
            var all = await db.BusinessAddresses.Where(x => x.BusinessId == businessId && x.AddressType == type).OrderBy(x => x.Id).ToListAsync(ct);
            var address = all.SingleOrDefault(x => x.Id == id);
            if (address is null) return Results.NotFound();
            db.BusinessAddresses.Remove(address); // orders keep their own address snapshot
            if (address.IsDefault) all.FirstOrDefault(x => x.Id != id)?.SetDefault();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static void SetDefault(this BusinessAddress address) => address.IsDefault = true;

    private static AddressType TypeOf(ClaimsPrincipal principal) => principal.IsInRole("Manufacturer") ? AddressType.Pickup : AddressType.Delivery;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Returns the city id for the pincode from the postal directory, or null when the street line is bad.
    /// A pincode that is not six digits, or not in the directory, throws pincode_invalid or pincode_unknown (400) so the screen can say what is wrong.
    /// </summary>
    private static async Task<long?> ValidateAsync(AddressRequest request, MarketplaceDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Line1) || request.Line1.Trim().Length > 200 || request.Line2?.Length > 200) return null;
        return await PostalDirectory.RequireCityIdAsync(db, request.Pincode, ct);
    }

    private static async Task<List<AddressResponse>> ListAsync(MarketplaceDbContext db, long businessId, AddressType type, CancellationToken ct)
        => await (from a in db.BusinessAddresses.AsNoTracking()
                  join c in db.Cities on a.CityId equals c.Id
                  join s in db.States on c.StateId equals s.Id
                  where a.BusinessId == businessId && a.AddressType == type
                  orderby a.IsDefault descending, a.Id
                  select new AddressResponse(a.Id, a.AddressType.ToString(), a.Line1, a.Line2, a.Pincode, c.Id, c.Name, s.Name, a.IsDefault)).ToListAsync(ct);

    private static async Task<AccountResponse> ProfileAsync(ApplicationUser user, UserManager<ApplicationUser> users, MarketplaceDbContext db, ICurrentBusiness current, CancellationToken ct)
    {
        BusinessProfileResponse? business = null;
        if (current.BusinessId is { } id && await db.Businesses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) is { } entity)
            business = BusinessEndpoints.Profile(entity, db);
        return new AccountResponse(user.Id, user.PhoneNumber, user.FullName, user.PreferredLanguage, (await users.GetRolesAsync(user)).ToList(), business);
    }
}
