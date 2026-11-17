using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGstInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                table: "OrderItems",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceType",
                table: "Invoices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.CreateTable(
                name: "InvoiceSequences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SellerBusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSequences_Businesses_SellerBusinessId",
                        column: x => x.SellerBusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices",
                columns: new[] { "SellerOrderId", "InvoiceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequences_SellerBusinessId_FinancialYear",
                table: "InvoiceSequences",
                columns: new[] { "SellerBusinessId", "FinancialYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceSequences");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId_InvoiceType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                table: "OrderItems");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceType",
                table: "Invoices",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId",
                table: "Invoices",
                column: "SellerOrderId");
        }
    }
}
