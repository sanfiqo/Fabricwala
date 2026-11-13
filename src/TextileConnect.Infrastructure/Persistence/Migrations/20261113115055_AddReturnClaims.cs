using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReturnClaims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SellerOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SellerResponse = table.Column<string>(type: "longtext", nullable: true),
                    AdminDecision = table.Column<string>(type: "longtext", nullable: true),
                    ApprovedRefundAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnClaims_SellerOrders_SellerOrderId",
                        column: x => x.SellerOrderId,
                        principalTable: "SellerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReturnClaimPhotos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ReturnClaimId = table.Column<long>(type: "bigint", nullable: false),
                    FileKey = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnClaimPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnClaimPhotos_ReturnClaims_ReturnClaimId",
                        column: x => x.ReturnClaimId,
                        principalTable: "ReturnClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnClaimPhotos_ReturnClaimId",
                table: "ReturnClaimPhotos",
                column: "ReturnClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnClaims_SellerOrderId",
                table: "ReturnClaims",
                column: "SellerOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnClaimPhotos");

            migrationBuilder.DropTable(
                name: "ReturnClaims");
        }
    }
}
