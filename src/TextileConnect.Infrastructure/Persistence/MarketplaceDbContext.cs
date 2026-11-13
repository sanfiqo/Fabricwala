using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;

namespace TextileConnect.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<BusinessAddress> BusinessAddresses => Set<BusinessAddress>();
    public DbSet<BusinessDocument> BusinessDocuments => Set<BusinessDocument>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<PriceSlab> PriceSlabs => Set<PriceSlab>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SellerOrder> SellerOrders => Set<SellerOrder>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<PayoutAccount> PayoutAccounts => Set<PayoutAccount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

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
        builder.Entity<Category>().HasKey(x => x.Id);
        builder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(240).IsRequired();
            entity.Property(x => x.GstRate).HasPrecision(5, 2);
            entity.HasIndex(x => x.Slug).IsUnique();
        });
        builder.Entity<ProductVariant>().HasKey(x => x.Id);
        builder.Entity<ProductVariant>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<PriceSlab>().HasKey(x => x.Id);
        builder.Entity<PriceSlab>().Property(x => x.PricePerUnit).HasPrecision(18, 2);
        builder.Entity<ProductMedia>().HasKey(x => x.Id);
        builder.Entity<ShippingRate>().HasKey(x => new { x.FromCityId, x.ToCityId, x.RateType });
        builder.Entity<ShippingRate>().Property(x => x.Rate).HasPrecision(18, 2);
        builder.Entity<ShippingRate>().Property(x => x.MinCharge).HasPrecision(18, 2);
        builder.Entity<CartItem>().HasKey(x => x.Id);
        builder.Entity<CartItem>().HasIndex(x => new { x.RetailerBusinessId, x.VariantId }).IsUnique();
        builder.Entity<CartItem>().Property(x => x.AddedByUserId).HasMaxLength(450).IsRequired();
        builder.Entity<Order>().HasKey(x => x.Id);
        builder.Entity<Order>().Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<Order>().Property(x => x.PlacedByUserId).HasMaxLength(450).IsRequired();
        builder.Entity<Order>().Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().HasKey(x => x.Id);
        builder.Entity<SellerOrder>().Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.CommissionRate).HasPrecision(5, 4);
        builder.Entity<Shipment>().HasKey(x => x.Id);
        builder.Entity<Shipment>().HasIndex(x => x.SellerOrderId).IsUnique();
        builder.Entity<PayoutAccount>().HasKey(x => x.Id);
        builder.Entity<PayoutAccount>().HasIndex(x => x.BusinessId).IsUnique();
        builder.Entity<Payment>().HasKey(x => x.Id);
        builder.Entity<Payment>().HasIndex(x => x.GatewayOrderId).IsUnique();
        builder.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Settlement>().HasKey(x => x.Id);
        builder.Entity<Settlement>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Settlement>().Property(x => x.CommissionAmount).HasPrecision(18, 2);
        builder.Entity<Refund>().HasKey(x => x.Id);
        builder.Entity<Refund>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Invoice>().HasKey(x => x.Id);
        builder.Entity<Invoice>().HasIndex(x => x.InvoiceNumber).IsUnique();
    }
}
