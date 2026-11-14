using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NightOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SellerOrders",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Refunds",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Refunds",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "GatewayRefundId",
                table: "Refunds",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_SellerOrderId_Status",
                table: "Refunds",
                columns: new[] { "SellerOrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_SellerOrderId_Status",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "GatewayRefundId",
                table: "Refunds");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SellerOrders",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Refunds",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32);
        }
    }
}
