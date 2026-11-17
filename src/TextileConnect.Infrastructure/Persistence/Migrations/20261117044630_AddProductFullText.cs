using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Word search for the catalogue (MATCH ... AGAINST on these three columns, see ProductTextSearch). Not part of the EF model: EF cannot describe a full-text index.
            migrationBuilder.Sql("ALTER TABLE `Products` ADD FULLTEXT INDEX `FTX_Products_Search` (`Title`, `Description`, `Material`);");
        

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Products` DROP INDEX `FTX_Products_Search`;");
        

        }
    }
}
