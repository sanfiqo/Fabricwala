using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHotQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columns first, then the new indexes, then the old ones: MySQL refuses to drop an index that a foreign key still relies on.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Settlements",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_ManufacturerBusinessId_Status",
                table: "Settlements",
                columns: new[] { "ManufacturerBusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_Status_ReleaseAfter",
                table: "Settlements",
                columns: new[] { "Status", "ReleaseAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_ManufacturerBusinessId_PlacedAtUtc_Id",
                table: "SellerOrders",
                columns: new[] { "ManufacturerBusinessId", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_Status_AcceptDeadlineUtc",
                table: "SellerOrders",
                columns: new[] { "Status", "AcceptDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Status_Id",
                table: "Refunds",
                columns: new[] { "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerBusinessId_IsDeleted_UpdatedAtUtc",
                table: "Products",
                columns: new[] { "SellerBusinessId", "IsDeleted", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus_PlacedAtUtc",
                table: "Orders",
                columns: new[] { "PaymentStatus", "PlacedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RetailerBusinessId_PlacedAtUtc_Id",
                table: "Orders",
                columns: new[] { "RetailerBusinessId", "PlacedAtUtc", "Id" });

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerBusinessId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RetailerBusinessId_Id",
                table: "Orders");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Columns first, then the new indexes, then the old ones: MySQL refuses to drop an index that a foreign key still relies on.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Settlements",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerBusinessId",
                table: "Products",
                column: "SellerBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RetailerBusinessId_Id",
                table: "Orders",
                columns: new[] { "RetailerBusinessId", "Id" });

            migrationBuilder.DropIndex(
                name: "IX_Settlements_ManufacturerBusinessId_Status",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_Status_ReleaseAfter",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_SellerOrders_ManufacturerBusinessId_PlacedAtUtc_Id",
                table: "SellerOrders");

            migrationBuilder.DropIndex(
                name: "IX_SellerOrders_Status_AcceptDeadlineUtc",
                table: "SellerOrders");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_Status_Id",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerBusinessId_IsDeleted_UpdatedAtUtc",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentStatus_PlacedAtUtc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RetailerBusinessId_PlacedAtUtc_Id",
                table: "Orders");

        }
    }
}
