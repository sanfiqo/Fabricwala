using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Community;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class CommunityTests
{
    [Fact]
    public async Task Favourites_are_unique_and_isolated_by_business()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MarketplaceDbContext(options);
        var service = new EfCommunityService(db);
        Assert.True(await service.ToggleFavouriteAsync(1, 20));
        Assert.True(await service.ToggleFavouriteAsync(2, 20));
        Assert.Equal(1, await db.Favourites.CountAsync(x => x.RetailerBusinessId == 1));
        Assert.Equal(1, await db.Favourites.CountAsync(x => x.RetailerBusinessId == 2));
        Assert.False(await service.ToggleFavouriteAsync(1, 20));
        Assert.Equal(1, await db.Favourites.CountAsync(x => x.RetailerBusinessId == 2));
    }
}
