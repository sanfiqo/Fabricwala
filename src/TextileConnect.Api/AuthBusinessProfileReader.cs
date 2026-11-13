using Microsoft.EntityFrameworkCore;
using Sanfiqo.Identity;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed class AuthBusinessProfileReader(MarketplaceDbContext db) : IAuthBusinessProfileReader
{
    public async Task<AuthBusinessProfile?> ReadAsync(string userId, CancellationToken cancellationToken = default)
        => await db.BusinessUsers.Where(x => x.UserId == userId).Join(db.Businesses, x => x.BusinessId, x => x.Id,
            (_, business) => new AuthBusinessProfile(business.Id, business.BusinessType.ToString(), business.DisplayName, business.VerificationStatus.ToString(), business.RejectionNote))
            .SingleOrDefaultAsync(cancellationToken);
}
