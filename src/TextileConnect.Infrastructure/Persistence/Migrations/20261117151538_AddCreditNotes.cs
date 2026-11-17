using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices");

            migrationBuilder.AddColumn<long>(
                name: "RefundId",
                table: "Invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType_RefundId",
                table: "Invoices",
                columns: new[] { "SellerOrderId", "InvoiceType", "RefundId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType_RefundId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RefundId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices",
                columns: new[] { "SellerOrderId", "InvoiceType" },
                unique: true);
        }
    }
}
