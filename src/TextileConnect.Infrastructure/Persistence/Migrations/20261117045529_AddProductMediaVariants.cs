using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMediaVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardKey",
                table: "ProductMedia",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lqip",
                table: "ProductMedia",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainKey",
                table: "ProductMedia",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbKey",
                table: "ProductMedia",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardKey",
                table: "ProductMedia");

            migrationBuilder.DropColumn(
                name: "Lqip",
                table: "ProductMedia");

            migrationBuilder.DropColumn(
                name: "MainKey",
                table: "ProductMedia");

            migrationBuilder.DropColumn(
                name: "ThumbKey",
                table: "ProductMedia");
        }
    }
}
