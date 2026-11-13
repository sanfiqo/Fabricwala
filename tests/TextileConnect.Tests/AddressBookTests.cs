using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class AddressBookTests
{
    [Fact]
    public void Address_book_keeps_one_default_per_type_and_can_change_it()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new MarketplaceDbContext(options);
        var directory = new EfBusinessDirectory(db);
        directory.Create("owner", new Business { LegalName = "Retailer", DisplayName = "Retailer", Phone = "+919000000001" });
        var first = directory.AddAddress("owner", NewAddress("A"));
        var second = directory.AddAddress("owner", NewAddress("B"));

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
        Assert.Same(second, directory.SetDefaultAddress("owner", second.Id));
        Assert.False(db.BusinessAddresses.Single(x => x.Id == first.Id).IsDefault);
        Assert.True(db.BusinessAddresses.Single(x => x.Id == second.Id).IsDefault);
    }

    private static BusinessAddress NewAddress(string line) => new() { AddressType = AddressType.Delivery, Line1 = line, CityId = 1, Pincode = "110001" };
}
