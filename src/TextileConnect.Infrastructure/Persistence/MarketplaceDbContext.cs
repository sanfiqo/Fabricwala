using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;

namespace TextileConnect.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<BusinessAddress> BusinessAddresses => Set<BusinessAddress>();
    public DbSet<BusinessDocument> BusinessDocuments => Set<BusinessDocument>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Business>(entity =>
        {
            entity.ToTable("Businesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Gstin).HasMaxLength(15);
            entity.Property(x => x.Pan).HasMaxLength(10);
            entity.Property(x => x.Phone).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CommissionRateOverride).HasPrecision(5, 4);
            entity.HasIndex(x => x.Gstin).IsUnique();
        });
        builder.Entity<BusinessUser>().HasKey(x => new { x.BusinessId, x.UserId });
        builder.Entity<BusinessAddress>().HasKey(x => x.Id);
        builder.Entity<BusinessDocument>().HasKey(x => x.Id);
        builder.Entity<State>().HasKey(x => x.Id);
        builder.Entity<City>().HasKey(x => x.Id);
        builder.Entity<City>().HasIndex(x => new { x.StateId, x.Name }).IsUnique();
    }
}
