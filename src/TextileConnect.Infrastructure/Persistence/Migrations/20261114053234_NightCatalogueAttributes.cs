using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NightCatalogueAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Products",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "DispatchDays",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FabricSpec",
                table: "Products",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                table: "Products",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "Products",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnPolicy",
                table: "Products",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Products",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Products",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerUnitGrams",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_IsActive",
                table: "ProductVariants",
                columns: new[] { "ProductId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_IsDeleted_CategoryId_Id",
                table: "Products",
                columns: new[] { "Status", "IsDeleted", "CategoryId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMedia_ProductId_SortOrder_Id",
                table: "ProductMedia",
                columns: new[] { "ProductId", "SortOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceSlabs_ProductId_PricePerUnit",
                table: "PriceSlabs",
                columns: new[] { "ProductId", "PricePerUnit" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAddresses_BusinessId_AddressType_IsDefault_Id",
                table: "BusinessAddresses",
                columns: new[] { "BusinessId", "AddressType", "IsDefault", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_IsActive",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_IsDeleted_CategoryId_Id",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductMedia_ProductId_SortOrder_Id",
                table: "ProductMedia");

            migrationBuilder.DropIndex(
                name: "IX_PriceSlabs_ProductId_PricePerUnit",
                table: "PriceSlabs");

            migrationBuilder.DropIndex(
                name: "IX_BusinessAddresses_BusinessId_AddressType_IsDefault_Id",
                table: "BusinessAddresses");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DispatchDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FabricSpec",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReturnPolicy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WeightPerUnitGrams",
                table: "Products");
        }
    }
}
