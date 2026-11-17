using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUnitsSold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnitsSold",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_IsDeleted_UnitsSold_Id",
                table: "Products",
                columns: new[] { "Status", "IsDeleted", "UnitsSold", "Id" });

            // Existing products: the pieces of seller orders that were already delivered.
            migrationBuilder.Sql(@"UPDATE `Products` p SET p.`UnitsSold` = COALESCE((SELECT SUM(oi.`Quantity`) FROM `OrderItems` oi JOIN `SellerOrders` so ON so.`Id` = oi.`SellerOrderId` WHERE oi.`ProductId` = p.`Id` AND so.`Status` = 'Delivered'), 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Status_IsDeleted_UnitsSold_Id",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsSold",
                table: "Products");
        }
    }
}
