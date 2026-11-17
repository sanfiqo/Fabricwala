using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPriceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceFrom",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTo",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Fill the new columns for the products that exist: the lowest and the highest price of their tiers. From now on MarketplaceDbContext keeps them in step on every save.
            migrationBuilder.Sql(
                "UPDATE `Products` p SET " +
                "p.`PriceFrom` = (SELECT MIN(s.`PricePerUnit`) FROM `PriceSlabs` s WHERE s.`ProductId` = p.`Id`), " +
                "p.`PriceTo` = (SELECT MAX(s.`PricePerUnit`) FROM `PriceSlabs` s WHERE s.`ProductId` = p.`Id`)");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_IsDeleted_Id",
                table: "Products",
                columns: new[] { "Status", "IsDeleted", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_IsDeleted_PriceFrom_Id",
                table: "Products",
                columns: new[] { "Status", "IsDeleted", "PriceFrom", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Status_IsDeleted_Id",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_IsDeleted_PriceFrom_Id",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PriceFrom",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PriceTo",
                table: "Products");
        }
    }
}
