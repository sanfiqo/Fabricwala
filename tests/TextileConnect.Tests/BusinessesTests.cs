using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class BusinessesTests
{
    [Fact]
    public void Directory_returns_only_the_requesting_users_business()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new MarketplaceDbContext(options);
        var directory = new EfBusinessDirectory(db);
        directory.Create("user-a", NewBusiness("A"));
        directory.Create("user-b", NewBusiness("B"));

        Assert.Equal("A", directory.GetForUser("user-a")?.DisplayName);
        Assert.Null(directory.GetForUser("user-c"));
    }

    [Fact]
    public async Task Unapproved_business_does_not_satisfy_policy()
    {
        var handler = new ApprovedBusinessHandler();
        var context = new AuthorizationHandlerContext([new ApprovedBusinessRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity()), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static Business NewBusiness(string name) => new() { LegalName = name, DisplayName = name, Phone = "+919876543210" };
}
