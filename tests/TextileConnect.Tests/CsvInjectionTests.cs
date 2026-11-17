using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class CsvInjectionTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Kapoor Knits", "Kapoor Knits")]
    [InlineData("=1+1", "'=1+1")]                                    // would be a formula
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTabbed", "'\tTabbed")]
    [InlineData("Sharma, Gupta & Sons", "\"Sharma, Gupta & Sons\"")]   // a comma would add a column
    [InlineData("The \"Best\" Shop", "\"The \"\"Best\"\" Shop\"")]
    [InlineData("Two\nLines", "\"Two\nLines\"")]                      // a line break would add a row
    [InlineData("=HYPERLINK(\"http://evil.example\",\"x\"),y", "\"'=HYPERLINK(\"\"http://evil.example\"\",\"\"x\"\"),y\"")]
    public void A_text_cell_cannot_run_as_a_formula_or_add_columns_or_rows(string? value, string expected)
        => Assert.Equal(expected, CsvCell.Text(value));

    [Fact]
    public async Task The_business_report_keeps_one_row_and_five_columns_whatever_a_business_is_calledAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAdminReportEndpoints());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Businesses.Add(new Business { BusinessType = BusinessType.Retailer, LegalName = "Hostile", DisplayName = "=HYPERLINK(\"http://evil.example\",\"x\"),Approved,extra", Phone = "+919999900001" });
            db.Businesses.Add(new Business { BusinessType = BusinessType.Manufacturer, LegalName = "Normal", DisplayName = "Kapoor Knits", Phone = "+919999900002" });
            await db.SaveChangesAsync();
        }
        using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var csv = await admin.GetStringAsync("/api/v1/admin/reports/businesses");

        var reader = new StringReader(csv);
        var rows = new List<string[]>();
        for (var row = PincodeImporter_ReadRow(reader); row is not null; row = PincodeImporter_ReadRow(reader)) rows.Add(row);
        Assert.Equal(3, rows.Count);                                    // the header and the two businesses, no extra row
        Assert.All(rows, row => Assert.Equal(5, row.Length));          // and no extra column
        var hostile = rows.Single(x => x[1].Contains("HYPERLINK"));
        Assert.StartsWith("'=", hostile[1]);                            // shown as text, never run as a formula
        Assert.Equal("Kapoor Knits", rows.Single(x => x[0] != "id" && x[1] == "Kapoor Knits")[1]);
    }

    private static string[]? PincodeImporter_ReadRow(TextReader reader) => PincodeImporter.ReadRow(reader);
}
