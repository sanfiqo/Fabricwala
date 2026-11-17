using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Domain.Community;
using TextileConnect.Domain.Notifications;

namespace TextileConnect.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<BusinessAddress> BusinessAddresses => Set<BusinessAddress>();
    public DbSet<BusinessDocument> BusinessDocuments => Set<BusinessDocument>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<PostalCode> PostalCodes => Set<PostalCode>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<PriceSlab> PriceSlabs => Set<PriceSlab>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
    public DbSet<DeliveryZoneRate> DeliveryZoneRates => Set<DeliveryZoneRate>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SellerOrder> SellerOrders => Set<SellerOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderRating> OrderRatings => Set<OrderRating>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<PayoutAccount> PayoutAccounts => Set<PayoutAccount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReturnClaim> ReturnClaims => Set<ReturnClaim>();
    public DbSet<ReturnClaimPhoto> ReturnClaimPhotos => Set<ReturnClaimPhoto>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Domain.Rfq.Rfq> Rfqs => Set<Domain.Rfq.Rfq>();
    public DbSet<Domain.Rfq.RfqQuotation> RfqQuotations => Set<Domain.Rfq.RfqQuotation>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<Favourite> Favourites => Set<Favourite>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BeforeSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BeforeSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Refuses to write a status text that is not one of the known constants (a typo would otherwise be stored silently), then stamps the times.</summary>
    private void BeforeSave()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case SellerOrder x: RequireKnown(x.Status, SellerOrderStatus.All, "seller order"); break;
                case Domain.Rfq.Rfq x: RequireKnown(x.Status, Domain.Rfq.RfqStatus.All, "request for quotation"); break;
                case Domain.Rfq.RfqQuotation x: RequireKnown(x.Status, Domain.Rfq.QuotationStatus.All, "quotation"); break;
                case OrderStatusHistory x: RequireKnown(x.Status, SellerOrderStatus.All, "order history"); break;
                case Order x: RequireKnown(x.PaymentStatus, PaymentStatus.All, "order payment"); break;
                case Payment x: RequireKnown(x.Status, PaymentStatus.All, "payment"); break;
                case Settlement x: RequireKnown(x.Status, SettlementStatus.All, "settlement"); break;
                case Refund x: RequireKnown(x.Status, RefundStatus.All, "refund"); break;
                case Shipment x: RequireKnown(x.Status, ShipmentProgress.All, "shipment"); break;
            }
        }
        Stamp();
    }

    private static void RequireKnown(string status, IReadOnlyList<string> known, string what)
    {
        if (!known.Contains(status)) throw new InvalidOperationException($"'{status}' is not a valid {what} status. Use the constants in TextileConnect.Domain.");
    }

    /// <summary>New rows get CreatedAtUtc and UpdatedAtUtc, changed rows get a new UpdatedAtUtc.</summary>
    private void Stamp()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<TextileConnect.Domain.ITimestamped>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default) entry.Entity.CreatedAtUtc = now;
                if (entry.Entity.UpdatedAtUtc == default) entry.Entity.UpdatedAtUtc = entry.Entity.CreatedAtUtc;
            }
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAtUtc = now;
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ConfigureRfq(builder);
        ConfigureRelationships(builder);
        ConfigureHotQueryIndexes(builder);
        builder.Entity<Business>(entity =>
        {
            entity.ToTable("Businesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OwnerName).HasMaxLength(120);
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Gstin).HasMaxLength(15);
            entity.Property(x => x.Pan).HasMaxLength(10);
            entity.Property(x => x.Phone).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CommissionRateOverride).HasPrecision(5, 4);
            entity.HasIndex(x => x.Gstin).IsUnique();
        });
        builder.Entity<BusinessUser>().HasKey(x => new { x.BusinessId, x.UserId });
        builder.Entity<BusinessUser>().HasIndex(x => x.UserId).IsUnique();
        builder.Entity<BusinessAddress>().HasKey(x => x.Id);
        builder.Entity<BusinessDocument>().HasKey(x => x.Id);
        builder.Entity<State>().HasKey(x => x.Id);
        builder.Entity<City>().HasKey(x => x.Id);
        builder.Entity<City>().HasIndex(x => new { x.StateId, x.Name }).IsUnique();
        builder.Entity<PostalCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Pincode).HasMaxLength(6).IsRequired();
            entity.HasIndex(x => x.Pincode).IsUnique();
            entity.HasOne<City>().WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DeliveryZone>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PincodePrefix).HasMaxLength(6).IsRequired();
            entity.HasIndex(x => x.PincodePrefix).IsUnique();
        });
        builder.Entity<Category>().HasKey(x => x.Id);
        builder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<HomeBanner>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired(); entity.Property(x => x.Link).HasMaxLength(1000); });
        builder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Material).HasMaxLength(100);
            entity.Property(x => x.FabricSpec).HasMaxLength(500);
            entity.Property(x => x.Season).HasMaxLength(80);
            entity.Property(x => x.HsnCode).HasMaxLength(20);
            entity.Property(x => x.WeightPerUnitGrams).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.Status, x.IsDeleted, x.CategoryId, x.Id });
            entity.Property(x => x.Slug).HasMaxLength(240).IsRequired();
            entity.Property(x => x.GstRate).HasPrecision(5, 2);
            entity.HasIndex(x => x.Slug).IsUnique();
        });
        builder.Entity<ProductVariant>().HasKey(x => x.Id);
        builder.Entity<ProductVariant>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<ProductVariant>().HasIndex(x => new { x.ProductId, x.IsActive });
        builder.Entity<PriceSlab>().HasIndex(x => new { x.ProductId, x.PricePerUnit });
        builder.Entity<ProductMedia>().HasIndex(x => new { x.ProductId, x.SortOrder, x.Id });
        builder.Entity<BusinessAddress>().HasIndex(x => new { x.BusinessId, x.AddressType, x.IsDefault, x.Id });
        builder.Entity<PriceSlab>().HasKey(x => x.Id);
        builder.Entity<PriceSlab>().Property(x => x.PricePerUnit).HasPrecision(18, 2);
        builder.Entity<ProductMedia>().HasKey(x => x.Id);
        builder.Entity<ProductMedia>().Property(x => x.MainKey).HasMaxLength(300);
        builder.Entity<ProductMedia>().Property(x => x.CardKey).HasMaxLength(300);
        builder.Entity<ProductMedia>().Property(x => x.ThumbKey).HasMaxLength(300);
        builder.Entity<ProductMedia>().Property(x => x.Lqip).HasMaxLength(2000);
        builder.Entity<ShippingRate>().HasKey(x => new { x.FromCityId, x.ToCityId, x.RateType });
        builder.Entity<ShippingRate>().Property(x => x.Rate).HasPrecision(18, 2);
        builder.Entity<ShippingRate>().Property(x => x.MinCharge).HasPrecision(18, 2);
        builder.Entity<DeliveryZoneRate>(entity =>
        {
            entity.HasKey(x => new { x.FromZoneId, x.ToZoneId, x.RateType });
            entity.Property(x => x.Rate).HasPrecision(18, 2);
            entity.Property(x => x.MinCharge).HasPrecision(18, 2);
        });
        builder.Entity<CartItem>().HasKey(x => x.Id);
        builder.Entity<CartItem>().HasIndex(x => new { x.RetailerBusinessId, x.VariantId }).IsUnique();
        builder.Entity<CartItem>().Property(x => x.AddedByUserId).HasMaxLength(450).IsRequired();
        builder.Entity<Order>().HasKey(x => x.Id);
        builder.Entity<Order>().Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<Order>().Property(x => x.PlacedByUserId).HasMaxLength(450).IsRequired();
        builder.Entity<Order>().Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().HasKey(x => x.Id);
        builder.Entity<SellerOrder>().Property(x => x.Status).HasMaxLength(40).IsConcurrencyToken();
        builder.Entity<OrderStatusHistory>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Status).HasMaxLength(40).IsRequired(); entity.Property(x => x.Note).HasMaxLength(1000); entity.HasIndex(x => new { x.SellerOrderId, x.ChangedAtUtc, x.Id }); });
        builder.Entity<OrderRating>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Comment).HasMaxLength(1000); entity.HasIndex(x => x.OrderId).IsUnique(); });
        builder.Entity<IdempotencyRecord>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Scope).HasMaxLength(40).IsRequired(); entity.Property(x => x.Key).HasMaxLength(100).IsRequired(); entity.Property(x => x.RequestHash).HasMaxLength(64).IsRequired(); entity.Property(x => x.ResourceKey).HasMaxLength(100).IsRequired(); entity.HasIndex(x => new { x.BusinessId, x.Scope, x.Key }).IsUnique(); });
        builder.Entity<OrderItem>().Property(x => x.Title).HasMaxLength(200);
        builder.Entity<OrderItem>().Property(x => x.Size).HasMaxLength(40);
        builder.Entity<OrderItem>().Property(x => x.Colour).HasMaxLength(40);
        builder.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Entity<OrderItem>().Property(x => x.GstRate).HasPrecision(5, 2);
        builder.Entity<OrderItem>().Property(x => x.GstAmount).HasPrecision(18, 2);
        builder.Entity<Order>().Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Entity<Order>().Property(x => x.ShippingTotal).HasPrecision(18, 2);
        builder.Entity<Order>().Property(x => x.TaxTotal).HasPrecision(18, 2);
        builder.Entity<Order>().Property(x => x.DeliveryLine1).HasMaxLength(200);
        builder.Entity<Order>().Property(x => x.DeliveryLine2).HasMaxLength(200);
        builder.Entity<Order>().Property(x => x.DeliveryCity).HasMaxLength(100);
        builder.Entity<Order>().Property(x => x.DeliveryPincode).HasMaxLength(6);
        builder.Entity<SellerOrder>().Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.CommissionAmount).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.CommissionGst).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.Total).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().HasIndex(x => new { x.ManufacturerBusinessId, x.Status, x.Id });
        builder.Entity<ProductVariant>().Property(x => x.StockQty).IsConcurrencyToken();
        builder.Entity<SellerOrder>().Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Entity<SellerOrder>().Property(x => x.CommissionRate).HasPrecision(5, 4);
        builder.Entity<Shipment>().HasKey(x => x.Id);
        builder.Entity<Shipment>().HasIndex(x => x.SellerOrderId).IsUnique();
        builder.Entity<ReturnClaim>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired(); entity.Property(x => x.Status).HasMaxLength(32).IsRequired(); entity.Property(x => x.ApprovedRefundAmount).HasPrecision(18, 2); });
        builder.Entity<ReturnClaimPhoto>().HasKey(x => x.Id);
        builder.Entity<Notification>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.UserId).HasMaxLength(450).IsRequired(); entity.Property(x => x.Title).HasMaxLength(200).IsRequired(); entity.Property(x => x.Message).HasMaxLength(2000).IsRequired(); entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc }); });
        builder.Entity<PayoutAccount>().HasKey(x => x.Id);
        builder.Entity<PayoutAccount>().HasIndex(x => x.BusinessId).IsUnique();
        builder.Entity<Payment>().HasKey(x => x.Id);
        builder.Entity<Payment>().Property(x => x.Status).HasMaxLength(32).IsConcurrencyToken();
        builder.Entity<Payment>().HasIndex(x => x.GatewayOrderId).IsUnique();
        builder.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Settlement>().HasKey(x => x.Id);
        builder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Gateway).HasMaxLength(32).IsRequired();
            entity.Property(x => x.EventId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.Gateway, x.EventId }).IsUnique();
        });
        builder.Entity<Settlement>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Settlement>().Property(x => x.CommissionAmount).HasPrecision(18, 2);
        builder.Entity<Refund>().HasKey(x => x.Id);
        builder.Entity<Refund>().Property(x => x.Status).HasMaxLength(32);
        builder.Entity<Refund>().Property(x => x.GatewayRefundId).HasMaxLength(100);
        builder.Entity<Refund>().HasIndex(x => new { x.SellerOrderId, x.Status });
        builder.Entity<Refund>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Invoice>().HasKey(x => x.Id);
        builder.Entity<Invoice>().HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Entity<Invoice>().Property(x => x.InvoiceNumber).HasMaxLength(32);
        builder.Entity<Invoice>().Property(x => x.InvoiceType).HasMaxLength(32);
        builder.Entity<Invoice>().HasIndex(x => new { x.SellerOrderId, x.InvoiceType, x.RefundId }).IsUnique();
        foreach (var money in new System.Linq.Expressions.Expression<Func<Invoice, decimal?>>[] { x => x.Taxable, x => x.Shipping, x => x.Cgst, x => x.Sgst, x => x.Igst, x => x.Total })
            builder.Entity<Invoice>().Property(money).HasPrecision(18, 2);
        builder.Entity<Invoice>().Property(x => x.CounterpartyName).HasMaxLength(200);
        builder.Entity<Invoice>().Property(x => x.CounterpartyGstin).HasMaxLength(15);
        builder.Entity<Invoice>().Property(x => x.IssuerGstin).HasMaxLength(15);
        builder.Entity<Invoice>().Property(x => x.PlaceOfSupplyCode).HasMaxLength(2);
        builder.Entity<Invoice>().HasIndex(x => x.IssuedAt);
        builder.Entity<InvoiceSequence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FinancialYear).HasMaxLength(4).IsRequired();
            entity.Property(x => x.LastNumber).IsConcurrencyToken();
            entity.Property(x => x.Series).HasMaxLength(12).IsRequired();
            entity.HasIndex(x => new { x.Series, x.FinancialYear }).IsUnique();
        });
        builder.Entity<OrderItem>().Property(x => x.HsnCode).HasMaxLength(8);
        builder.Entity<OrderItem>().Property(x => x.ImageUrl).HasMaxLength(400);
        builder.Entity<AuditLog>().HasKey(x => x.Id);
        builder.Entity<AuditLog>().Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Entity<AuditLog>().Property(x => x.EntityId).HasMaxLength(64);
        builder.Entity<AuditLog>().Property(x => x.IpAddress).HasMaxLength(45);
        builder.Entity<AuditLog>().Property(x => x.Details).HasMaxLength(1000);
        builder.Entity<AuditLog>().HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAtUtc });
        builder.Entity<AuditLog>().Property(x => x.EntityName).HasMaxLength(200).IsRequired();
        builder.Entity<PlatformSetting>().HasKey(x => x.Key);
        builder.Entity<PlatformSetting>().Property(x => x.Key).HasMaxLength(200);
        builder.Entity<PlatformSetting>().Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.Entity<Favourite>().HasKey(x => x.Id);
        builder.Entity<Favourite>().HasIndex(x => new { x.RetailerBusinessId, x.ProductId }).IsUnique();
    }

    /// <summary>
    /// Every relation between marketplace tables is a real foreign key. Delete is always Restrict: rows are soft-deleted or kept, never cascaded.
    /// Notification.UserId and the user columns point to the identity database and are deliberately not foreign keys.
    /// docs/FK_ORPHAN_CHECK.sql is generated from this model; a test keeps it in step.
    /// </summary>
    /// <summary>Requests for quotation (docs/RFQ.md).</summary>
    private static void ConfigureRfq(ModelBuilder builder)
    {
        builder.Entity<Domain.Rfq.Rfq>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.TargetPricePerUnit).HasPrecision(18, 2);
            entity.Property(x => x.DeliveryPincode).HasMaxLength(6).IsRequired();
            entity.Property(x => x.DeliveryCity).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DeliveryState).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsConcurrencyToken();
            entity.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
            entity.HasIndex(x => new { x.RetailerBusinessId, x.Status });
        });
        builder.Entity<Domain.Rfq.RfqQuotation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PricePerUnit).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.HasIndex(x => new { x.RfqId, x.SellerBusinessId }).IsUnique();
            entity.HasIndex(x => new { x.SellerBusinessId, x.Status });
        });
    }

    private static void ConfigureRelationships(ModelBuilder builder)
    {
        void Link<TDependent, TPrincipal>(System.Linq.Expressions.Expression<Func<TDependent, object?>> foreignKey)
            where TDependent : class where TPrincipal : class
            => builder.Entity<TDependent>().HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);

        // Businesses
        Link<BusinessUser, Business>(x => x.BusinessId);
        Link<BusinessAddress, Business>(x => x.BusinessId);
        Link<BusinessAddress, City>(x => x.CityId);
        Link<BusinessDocument, Business>(x => x.BusinessId);
        Link<PayoutAccount, Business>(x => x.BusinessId);
        Link<City, State>(x => x.StateId);
        // Catalogue
        Link<Product, Business>(x => x.SellerBusinessId);
        Link<Product, Category>(x => x.CategoryId);
        Link<ProductVariant, Product>(x => x.ProductId);
        Link<PriceSlab, Product>(x => x.ProductId);
        Link<ProductMedia, Product>(x => x.ProductId);
        Link<ShippingRate, City>(x => x.FromCityId);
        Link<ShippingRate, City>(x => x.ToCityId);
        Link<DeliveryZoneRate, DeliveryZone>(x => x.FromZoneId);
        Link<DeliveryZoneRate, DeliveryZone>(x => x.ToZoneId);
        // Relations that have navigation properties are configured with them, so EF does not add a second foreign key column.
        builder.Entity<SellerOrder>().HasOne(x => x.Order).WithMany(x => x.SellerOrders).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SellerOrder>().HasMany(x => x.Items).WithOne(x => x.SellerOrder).HasForeignKey(x => x.SellerOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SellerOrder>().HasMany(x => x.History).WithOne(x => x.SellerOrder).HasForeignKey(x => x.SellerOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ReturnClaim>().HasOne(x => x.SellerOrder).WithMany().HasForeignKey(x => x.SellerOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ReturnClaim>().HasMany(x => x.Photos).WithOne().HasForeignKey(x => x.ReturnClaimId).OnDelete(DeleteBehavior.Restrict);
        // Cart and orders
        Link<CartItem, ProductVariant>(x => x.VariantId);
        Link<Order, Business>(x => x.RetailerBusinessId);
        Link<SellerOrder, Business>(x => x.ManufacturerBusinessId);
        Link<OrderItem, Product>(x => x.ProductId);
        Link<OrderItem, ProductVariant>(x => x.VariantId);
        Link<Shipment, SellerOrder>(x => x.SellerOrderId);
        // Money
        Link<Payment, Order>(x => x.OrderId);
        Link<Settlement, Payment>(x => x.PaymentId);
        Link<Settlement, SellerOrder>(x => x.SellerOrderId);
        Link<Refund, Payment>(x => x.PaymentId);
        Link<Refund, SellerOrder>(x => x.SellerOrderId);
        Link<Invoice, SellerOrder>(x => x.SellerOrderId);
        // Requests for quotation
        Link<Domain.Rfq.Rfq, Business>(x => x.RetailerBusinessId);
        Link<Domain.Rfq.Rfq, Category>(x => x.CategoryId);
        Link<Domain.Rfq.RfqQuotation, Domain.Rfq.Rfq>(x => x.RfqId);
        Link<Domain.Rfq.RfqQuotation, Business>(x => x.SellerBusinessId);
        // Saved products
        Link<Favourite, Business>(x => x.RetailerBusinessId);
        Link<Favourite, Product>(x => x.ProductId);
    }

    /// <summary>
    /// Indexes for the queries that run on every screen or every minute. docs/INDEXES.md says which query each one serves.
    /// Foreign key columns are already indexed (EF adds one for each relation), so only composite indexes are listed here.
    /// </summary>
    private static void ConfigureHotQueryIndexes(ModelBuilder builder)
    {
        builder.Entity<Order>().HasIndex(x => new { x.RetailerBusinessId, x.PlacedAtUtc, x.Id });
        builder.Entity<Order>().HasIndex(x => new { x.PaymentStatus, x.PlacedAtUtc });
        builder.Entity<SellerOrder>().HasIndex(x => new { x.Status, x.AcceptDeadlineUtc });
        builder.Entity<SellerOrder>().HasIndex(x => new { x.ManufacturerBusinessId, x.PlacedAtUtc, x.Id });
        builder.Entity<Settlement>().HasIndex(x => new { x.Status, x.ReleaseAfter });
        builder.Entity<Settlement>().HasIndex(x => new { x.ManufacturerBusinessId, x.Status });
        builder.Entity<Refund>().HasIndex(x => new { x.Status, x.Id });
        builder.Entity<Product>().HasIndex(x => new { x.SellerBusinessId, x.IsDeleted, x.UpdatedAtUtc });
        builder.Entity<Product>().HasIndex(x => new { x.Status, x.IsDeleted, x.UnitsSold, x.Id });
    }
}
