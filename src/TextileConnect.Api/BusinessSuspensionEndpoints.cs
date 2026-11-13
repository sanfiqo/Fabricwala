using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sanfiqo.Identity;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class BusinessSuspensionEndpoints
{
    public static void MapBusinessSuspensionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/businesses").RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapPost("/{id:long}/suspension", async (long id, SuspensionRequest request, MarketplaceDbContext db, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var business = await db.Businesses.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (business is null || request.Action is not ("Suspend" or "Reactivate")) return Results.BadRequest();
            business.VerificationStatus = request.Action == "Suspend" ? VerificationStatus.Suspended : VerificationStatus.Approved;
            var userIds = await db.BusinessUsers.Where(x => x.BusinessId == id).Select(x => x.UserId).ToListAsync(ct);
            foreach (var userId in userIds)
            {
                var user = await users.FindByIdAsync(userId); if (user is null) continue;
                user.IsActive = request.Action == "Reactivate"; await users.UpdateAsync(user); await users.UpdateSecurityStampAsync(user);
            }
            await db.SaveChangesAsync(ct); return Results.Ok(new { business.Id, business.VerificationStatus, action = request.Action });
        });
    }
    public sealed record SuspensionRequest(string Action);
}
