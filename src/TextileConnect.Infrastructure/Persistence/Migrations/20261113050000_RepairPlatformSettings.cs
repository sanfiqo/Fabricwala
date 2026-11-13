using Microsoft.EntityFrameworkCore.Migrations;

namespace TextileConnect.Infrastructure.Persistence.Migrations;

[Migration("20261113050000_RepairPlatformSettings")]
public partial class RepairPlatformSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS `PlatformSettings` (`Key` varchar(200) NOT NULL, `Value` varchar(4000) NOT NULL, `UpdatedByUserId` varchar(450) NULL, `UpdatedAtUtc` datetime(6) NOT NULL, CONSTRAINT `PK_PlatformSettings` PRIMARY KEY (`Key`)) CHARACTER SET utf8mb4;");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP TABLE IF EXISTS `PlatformSettings`;");
}
