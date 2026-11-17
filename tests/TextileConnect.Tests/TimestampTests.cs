using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Community;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class TimestampTests
{
    private static MarketplaceDbContext NewContext(string name)
        => new(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task New_rows_get_both_times_and_changed_rows_get_a_new_update_timeAsync()
    {
        var name = Guid.NewGuid().ToString();
        var before = DateTime.UtcNow.AddSeconds(-1);
        await using (var db = NewContext(name))
        {
            db.Favourites.Add(new Favourite { RetailerBusinessId = 1, ProductId = 2, CreatedAtUtc = default, UpdatedAtUtc = default });
            db.Settlements.Add(new Settlement { PaymentId = 1, SellerOrderId = 1, ManufacturerBusinessId = 1, Amount = 10m });
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext(name))
        {
            var favourite = await db.Favourites.SingleAsync();
            Assert.True(favourite.CreatedAtUtc > before);
            Assert.Equal(favourite.CreatedAtUtc, favourite.UpdatedAtUtc);

            var settlement = await db.Settlements.SingleAsync();
            var created = settlement.CreatedAtUtc;
            settlement.UpdatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc); // an old value that the save must replace
            settlement.Status = "Released";
            await db.SaveChangesAsync();

            Assert.Equal(created, settlement.CreatedAtUtc);
            Assert.True(settlement.UpdatedAtUtc > before);
        }
    }

    [Fact]
    public void Synchronous_saves_are_stamped_too()
    {
        using var db = NewContext(Guid.NewGuid().ToString());
        db.Favourites.Add(new Favourite { RetailerBusinessId = 1, ProductId = 2, CreatedAtUtc = default, UpdatedAtUtc = default });
        db.SaveChanges();
        Assert.True(db.Favourites.Single().CreatedAtUtc > DateTime.UtcNow.AddSeconds(-5));
    }
}
