using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Settlements",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Settlements",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Reviews",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Reviews",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Quotations",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Quotations",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "LogisticsAssignments",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "LogisticsAssignments",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "GroupBuys",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "GroupBuys",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "GroupBuyParticipants",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "GroupBuyParticipants",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Favourites",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Favourites",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Enquiries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Enquiries",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Disputes",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Disputes",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Rows that already exist get "now" as their created and changed time (the real time is not known).
            migrationBuilder.Sql("UPDATE `Disputes` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `Enquiries` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `Favourites` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `GroupBuyParticipants` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `GroupBuys` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `LogisticsAssignments` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `Quotations` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `Reviews` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
            migrationBuilder.Sql("UPDATE `Settlements` SET `CreatedAtUtc` = UTC_TIMESTAMP(6), `UpdatedAtUtc` = UTC_TIMESTAMP(6);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "LogisticsAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "LogisticsAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "GroupBuys");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "GroupBuys");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "GroupBuyParticipants");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "GroupBuyParticipants");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Favourites");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Favourites");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Disputes");
        }
    }
}
