using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cgst",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyGstin",
                table: "Invoices",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyName",
                table: "Invoices",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Igst",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuerGstin",
                table: "Invoices",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfSupplyCode",
                table: "Invoices",
                type: "varchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Sgst",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Shipping",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Taxable",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssuedAt",
                table: "Invoices",
                column: "IssuedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_IssuedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Cgst",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CounterpartyGstin",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CounterpartyName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Igst",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IssuerGstin",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlaceOfSupplyCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Sgst",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Shipping",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Taxable",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Invoices");
        }
    }
}
