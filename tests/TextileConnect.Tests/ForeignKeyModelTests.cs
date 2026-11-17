using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>
/// Pins the foreign keys of the marketplace schema and keeps docs/FK_ORPHAN_CHECK.sql (generated from the model) in step.
/// Run with FK_EXPORT=1 to rewrite the SQL file after the relations change.
/// </summary>
public sealed class ForeignKeyModelTests
{
    private static IReadOnlyList<IForeignKey> ForeignKeys()
    {
        // The MySQL provider only builds the model here; nothing connects to a database.
        using var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseMySQL("Server=localhost;Database=model_only;Uid=x;Pwd=x").Options);
        return ((IInfrastructure<IServiceProvider>)db).GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .SelectMany(x => x.GetForeignKeys()).OrderBy(Label, StringComparer.Ordinal).ToList();
    }

    private static string Label(IForeignKey key)
        => $"{key.DeclaringEntityType.GetTableName()}.{string.Join("+", key.Properties.Select(x => x.GetColumnName()))} -> {key.PrincipalEntityType.GetTableName()}.{string.Join("+", key.PrincipalKey.Properties.Select(x => x.GetColumnName()))}";

    [Fact]
    public void Every_marketplace_relation_is_a_restricting_foreign_key()
    {
        var keys = ForeignKeys();
        Assert.All(keys, key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        Assert.All(keys, key => Assert.Single(key.Properties));

        var labels = keys.Select(Label).ToHashSet();
        foreach (var expected in new[]
        {
            "Products.SellerBusinessId -> Businesses.Id", "Products.CategoryId -> Categories.Id", "ProductVariants.ProductId -> Products.Id",
            "PriceSlabs.ProductId -> Products.Id", "ProductMedia.ProductId -> Products.Id", "CartItems.VariantId -> ProductVariants.Id",
            "Orders.RetailerBusinessId -> Businesses.Id", "SellerOrders.ManufacturerBusinessId -> Businesses.Id", "SellerOrders.OrderId -> Orders.Id",
            "OrderItems.ProductId -> Products.Id", "OrderItems.VariantId -> ProductVariants.Id", "OrderItems.SellerOrderId -> SellerOrders.Id",
            "Payments.OrderId -> Orders.Id", "Settlements.PaymentId -> Payments.Id", "Settlements.SellerOrderId -> SellerOrders.Id",
            "Refunds.PaymentId -> Payments.Id", "Refunds.SellerOrderId -> SellerOrders.Id", "Invoices.SellerOrderId -> SellerOrders.Id",
            "Shipments.SellerOrderId -> SellerOrders.Id", "ReturnClaims.SellerOrderId -> SellerOrders.Id", "ReturnClaimPhotos.ReturnClaimId -> ReturnClaims.Id",
            "BusinessAddresses.BusinessId -> Businesses.Id", "BusinessAddresses.CityId -> Cities.Id", "BusinessDocuments.BusinessId -> Businesses.Id",
            "BusinessUsers.BusinessId -> Businesses.Id", "PayoutAccounts.BusinessId -> Businesses.Id", "Cities.StateId -> States.Id",
            "ShippingRates.FromCityId -> Cities.Id", "ShippingRates.ToCityId -> Cities.Id",
            "DeliveryZoneRates.FromZoneId -> DeliveryZones.Id", "DeliveryZoneRates.ToZoneId -> DeliveryZones.Id",
        })
            Assert.Contains(expected, labels);
        Assert.DoesNotContain(labels, x => x.StartsWith("Notifications."));
    }

    [Fact]
    public void Orphan_check_sql_matches_the_model_and_is_exported_on_request()
    {
        var sql = OrphanCheckSql(ForeignKeys());
        var file = Path.Combine(RepoRoot(), "docs", "FK_ORPHAN_CHECK.sql");
        if (Environment.GetEnvironmentVariable("FK_EXPORT") == "1") { File.WriteAllText(file, sql); return; }
        Assert.True(File.Exists(file), "docs/FK_ORPHAN_CHECK.sql is missing. Run this test once with FK_EXPORT=1.");
        Assert.True(File.ReadAllText(file).Replace("\r\n", "\n") == sql, "docs/FK_ORPHAN_CHECK.sql is out of date. Run this test with FK_EXPORT=1 and commit the file.");
    }

    [Fact]
    public void Data_model_doc_matches_the_model_and_is_exported_on_request()
    {
        var markdown = DataModelMarkdown();
        var file = Path.Combine(RepoRoot(), "docs", "DATA_MODEL.md");
        if (Environment.GetEnvironmentVariable("FK_EXPORT") == "1") { File.WriteAllText(file, markdown); return; }
        Assert.True(File.Exists(file), "docs/DATA_MODEL.md is missing. Run this test once with FK_EXPORT=1.");
        Assert.True(File.ReadAllText(file).Replace("\r\n", "\n") == markdown, "docs/DATA_MODEL.md is out of date. Run this test with FK_EXPORT=1 and commit the file.");
    }

    /// <summary>The tables, columns, foreign keys and indexes of the marketplace database, read from the EF model (what the migrations build).</summary>
    private static string DataModelMarkdown()
    {
        using var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseMySQL("Server=localhost;Database=model_only;Uid=x;Pwd=x").Options);
        var model = ((IInfrastructure<IServiceProvider>)db).GetService<IDesignTimeModel>().Model;
        var text = new StringBuilder();
        text.Append("# Data model\n\n");
        text.Append("Generated by `ForeignKeyModelTests` from the EF model of `MarketplaceDbContext`, which is what the migrations build; do not edit by hand. Run that test with `FK_EXPORT=1` after a change.\n");
        text.Append("The sign-in tables (users, roles, one-time codes, Data Protection keys) are in the second database of `Sanfiqo.Identity`; they are not listed. A business table never points at a user with a foreign key, only with the user's text id.\n");
        text.Append("Money is `decimal(18,2)`, times are UTC `datetime(6)`, enums are stored as numbers, statuses as the text constants of `TextileConnect.Domain`.\n\n");
        var tables = model.GetEntityTypes().Where(x => x.GetTableName() is not null).OrderBy(x => x.GetTableName(), StringComparer.Ordinal).ToList();
        text.Append($"{tables.Count} tables.\n");
        foreach (var entity in tables)
        {
            var key = entity.FindPrimaryKey();
            text.Append($"\n## {entity.GetTableName()}\n\n| Column | Type | Null | Notes |\n| --- | --- | --- | --- |\n");
            foreach (var property in entity.GetProperties())
            {
                var notes = new List<string>();
                if (key?.Properties.Contains(property) == true) notes.Add("key");
                if (property.ValueGenerated == ValueGenerated.OnAdd && key?.Properties.Contains(property) == true && key.Properties.Count == 1) notes.Add("auto number");
                if (property.IsConcurrencyToken) notes.Add("concurrency check");
                notes.AddRange(entity.GetForeignKeys().Where(f => f.Properties.Contains(property)).Select(f => $"-> {f.PrincipalEntityType.GetTableName()}.{f.PrincipalKey.Properties[0].GetColumnName()}"));
                text.Append($"| {property.GetColumnName()} | {property.GetColumnType()} | {(property.IsNullable ? "yes" : "no")} | {string.Join(", ", notes)} |\n");
            }
            var indexes = entity.GetIndexes().OrderBy(i => i.GetDatabaseName(), StringComparer.Ordinal).ToList();
            if (indexes.Count > 0)
                text.Append("\nIndexes: " + string.Join("; ", indexes.Select(i => $"{(i.IsUnique ? "unique " : string.Empty)}({string.Join(", ", i.Properties.Select(p => p.GetColumnName()))})")) + "\n");
        }
        return text.ToString();
    }

    private static string OrphanCheckSql(IReadOnlyList<IForeignKey> keys)
    {
        var text = new StringBuilder();
        text.Append("-- Orphan check for the foreign keys of the marketplace schema (generated from the EF model; do not edit by hand).\n");
        text.Append("-- Run this on a database BEFORE applying the migration that adds the foreign keys. It only reads.\n");
        text.Append("-- Every row with orphans > 0 will make the migration fail. For each one, either delete the orphan child rows or insert the missing parent,\n");
        text.Append("-- then run this again until it returns no rows. A fresh database has nothing to fix.\n");
        text.Append("-- Example: mysql -h <host> -u <user> -p <database> < docs/FK_ORPHAN_CHECK.sql\n\n");
        var queries = keys.Select(key =>
        {
            var child = key.DeclaringEntityType.GetTableName()!;
            var column = key.Properties[0].GetColumnName();
            var parent = key.PrincipalEntityType.GetTableName()!;
            var parentColumn = key.PrincipalKey.Properties[0].GetColumnName();
            var nullable = key.Properties[0].IsNullable;
            return $"SELECT '{Label(key)}' AS relation, COUNT(*) AS orphans FROM `{child}` c LEFT JOIN `{parent}` p ON p.`{parentColumn}` = c.`{column}` "
                 + $"WHERE p.`{parentColumn}` IS NULL{(nullable ? $" AND c.`{column}` IS NOT NULL" : string.Empty)}";
        }).ToList();
        text.Append("SELECT relation, orphans FROM (\n  ");
        text.Append(string.Join("\n  UNION ALL\n  ", queries));
        text.Append("\n) checks WHERE orphans > 0 ORDER BY relation;\n");
        return text.ToString();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TextileConnect.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }
}
