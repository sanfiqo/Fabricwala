using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NightCheckoutSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SellerOrders",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptDeadlineUtc",
                table: "SellerOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "SellerOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlacedAtUtc",
                table: "SellerOrders",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "SellerOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "SellerOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "SellerOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "Orders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLine1",
                table: "Orders",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLine2",
                table: "Orders",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPincode",
                table: "Orders",
                type: "varchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlacedAtUtc",
                table: "Orders",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingTotal",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Colour",
                table: "OrderItems",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstRate",
                table: "OrderItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "OrderItems",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "OrderItems",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "VariantId",
                table: "OrderItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Scope = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ResourceKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OrderStatusHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SellerOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_SellerOrders_SellerOrderId",
                        column: x => x.SellerOrderId,
                        principalTable: "SellerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_ManufacturerBusinessId_Status_Id",
                table: "SellerOrders",
                columns: new[] { "ManufacturerBusinessId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RetailerBusinessId_Id",
                table: "Orders",
                columns: new[] { "RetailerBusinessId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_BusinessId_Scope_Key",
                table: "IdempotencyRecords",
                columns: new[] { "BusinessId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_SellerOrderId_ChangedAtUtc_Id",
                table: "OrderStatusHistories",
                columns: new[] { "SellerOrderId", "ChangedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "OrderStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_SellerOrders_ManufacturerBusinessId_Status_Id",
                table: "SellerOrders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RetailerBusinessId_Id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AcceptDeadlineUtc",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "PlacedAtUtc",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "ShippingAmount",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "SellerOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLine1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLine2",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPincode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlacedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Colour",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "GstRate",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "OrderItems");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SellerOrders",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");
        }
    }
}
