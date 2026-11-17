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

            // The foreign key on SellerOrderId needs an index all the time: the new index is built before the old one is dropped.
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices",
                columns: new[] { "SellerOrderId", "InvoiceType" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType_RefundId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RefundId",
                table: "Invoices");
        }
    }
}
