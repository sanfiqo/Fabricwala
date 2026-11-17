using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class PincodeImportTests
{
    // The layout of the India Post directory: one row per post office, names in capitals, a comma inside a quoted name, old state names.
    private const string Sample = """
        circlename,regionname,divisionname,officename,pincode,officetype,delivery,district,statename,latitude,longitude
        Madhya Pradesh Circle,Indore Region,Indore Division,"Indore GPO, Main",452001,HO,Delivery,INDORE,MADHYA PRADESH,22.7,75.8
        Madhya Pradesh Circle,Indore Region,Indore Division,Rajwada S.O,452002,SO,Delivery,INDORE,MADHYA PRADESH,22.7,75.8
        Madhya Pradesh Circle,Indore Region,Indore Division,Khajrana B.O,452002,BO,Delivery,INDORE,MADHYA PRADESH,22.7,75.9
        Karnataka Circle,Bengaluru Region,Bengaluru Division,Bangalore GPO,560001,HO,Delivery,BANGALORE,KARNATAKA,12.9,77.5
        Delhi Circle,Delhi Region,Delhi Division,Connaught Place,110001,SO,Delivery,CENTRAL DELHI,DELHI,28.6,77.2
        J&K Circle,Srinagar Region,Srinagar Division,Lal Chowk,190001,SO,Delivery,SRINAGAR,JAMMU & KASHMIR,34.0,74.8
        Orissa Circle,Bhubaneswar Region,Bhubaneswar Division,Secretariat,751001,SO,Delivery,KHORDHA,ORISSA,20.2,85.8
        Punjab Circle,Ludhiana Region,Ludhiana Division,Ludhiana HO,141001,HO,Delivery,LUDHIANA,PUNJAB,30.9,75.8
        Nowhere Circle,X,X,Atlantis PO,999991,SO,Delivery,SUNKEN,ATLANTIS,0,0
        Bad Circle,X,X,Short PO,12345,SO,Delivery,INDORE,MADHYA PRADESH,0,0
        Bad Circle,X,X,Zero PO,052001,SO,Delivery,INDORE,MADHYA PRADESH,0,0
        Bad Circle,X,X,No district PO,452009,SO,Delivery,,MADHYA PRADESH,0,0

        Madhya Pradesh Circle,Indore Region,Indore Division,Other Indore,452003,SO,Delivery,indore,madhya pradesh,22.7,75.8
        """;

    private static async Task<MarketplaceDbContext> NewDatabaseAsync()
    {
        var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await MarketplaceSeed.SeedAsync(db);
        return db;
    }

    [Fact]
    public async Task The_directory_becomes_cities_and_pincodes_and_known_pincodes_are_left_aloneAsync()
    {
        await using var db = await NewDatabaseAsync();
        var indoreBefore = await db.Cities.SingleAsync(x => x.Name == "Indore");

        var result = await PincodeImporter.ImportAsync(db, new StringReader(Sample));

        Assert.Equal(13, result.RowsRead);
        Assert.Equal(4, result.RowsSkipped);                 // short pincode, leading zero, no district, unknown state; the blank line is not a row
        Assert.Equal(new Dictionary<string, int> { ["ATLANTIS"] = 1 }, result.UnknownStates);
        Assert.Equal(2, result.PincodesAlreadyKnown);        // 452001 and 141001 came with the seed
        Assert.Equal(6, result.PincodesAdded);               // 452002, 452003, 560001, 110001, 190001, 751001
        Assert.Equal(2 + 6, await db.PostalCodes.CountAsync());

        // 452001 still points at the city it always did; districts that match an existing city reuse it instead of adding a duplicate.
        Assert.Equal(indoreBefore.Id, (await db.PostalCodes.SingleAsync(x => x.Pincode == "452001")).CityId);
        Assert.Equal(indoreBefore.Id, (await db.PostalCodes.SingleAsync(x => x.Pincode == "452002")).CityId);
        Assert.Equal(indoreBefore.Id, (await db.PostalCodes.SingleAsync(x => x.Pincode == "452003")).CityId);
        Assert.Equal(1, await db.Cities.CountAsync(x => x.Name == "Indore"));

        // New districts become cities with a readable name in the right state.
        var khordha = await db.Cities.SingleAsync(x => x.Name == "Khordha");
        Assert.Equal("OD", (await db.States.SingleAsync(x => x.Id == khordha.StateId)).Code);   // "ORISSA" is now Odisha
        var srinagar = await db.Cities.SingleAsync(x => x.Name == "Srinagar");
        Assert.Equal("JK", (await db.States.SingleAsync(x => x.Id == srinagar.StateId)).Code);  // "JAMMU & KASHMIR"
        Assert.Equal(1, await db.Cities.CountAsync(x => x.Name == "Srinagar"));                   // the seeded Srinagar, not a second one
        Assert.Contains(await db.Cities.Select(x => x.Name).ToListAsync(), x => x == "Central Delhi");
        Assert.Equal(3, result.CitiesAdded);                  // Bangalore, Central Delhi, Khordha
    }

    [Fact]
    public async Task Running_it_again_adds_nothingAsync()
    {
        await using var db = await NewDatabaseAsync();
        await PincodeImporter.ImportAsync(db, new StringReader(Sample));
        var pincodes = await db.PostalCodes.CountAsync();
        var cities = await db.Cities.CountAsync();

        var second = await PincodeImporter.ImportAsync(db, new StringReader(Sample));

        Assert.Equal(0, second.PincodesAdded);
        Assert.Equal(0, second.CitiesAdded);
        Assert.Equal(8, second.PincodesAlreadyKnown);
        Assert.Equal(pincodes, await db.PostalCodes.CountAsync());
        Assert.Equal(cities, await db.Cities.CountAsync());
    }

    [Fact]
    public async Task A_pincode_that_post_offices_place_in_two_districts_goes_to_the_most_common_oneAsync()
    {
        await using var db = await NewDatabaseAsync();
        const string csv = "pincode,districtname,statename\n302030,JAIPUR,RAJASTHAN\n302030,JAIPUR,RAJASTHAN\n302030,TONK,RAJASTHAN\n";
        await PincodeImporter.ImportAsync(db, new StringReader(csv));
        var city = await db.PostalCodes.Where(x => x.Pincode == "302030").Join(db.Cities, p => p.CityId, c => c.Id, (_, c) => c.Name).SingleAsync();
        Assert.Equal("Jaipur", city);
    }

    [Theory]
    [InlineData("")]
    [InlineData("officename,code\nA,1\n")]
    [InlineData("pincode,district\n452001,INDORE\n")]
    public async Task A_file_without_the_needed_columns_is_refusedAsync(string csv)
    {
        await using var db = await NewDatabaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => PincodeImporter.ImportAsync(db, new StringReader(csv)));
    }

    [Fact]
    public async Task Imported_pincodes_work_for_the_lookup_and_for_signing_upAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using (var scope = fixture.Services.CreateScope())
            await PincodeImporter.ImportAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(), new StringReader(Sample));
        using var client = await fixture.ClientAsync("Retailer", "bengaluru-shop");

        var lookup = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/pincodes/560001");
        Assert.Equal("Bangalore", lookup.GetProperty("city").GetString());
        Assert.Equal("Karnataka", lookup.GetProperty("state").GetString());
        Assert.Equal(System.Net.HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Bengaluru Shop", displayName = "Bengaluru Shop", pincode = "560001" })).StatusCode);
    }

    [Theory]
    [InlineData("a,b,c\n1,2,3\n", new[] { "a", "b", "c", "1", "2", "3" })]
    [InlineData("\"x,y\",\"say \"\"hi\"\"\",z\r\n", new[] { "x,y", "say \"hi\"", "z" })]
    [InlineData("\"line\nbreak\",end", new[] { "line\nbreak", "end" })]
    [InlineData("a,,c", new[] { "a", "", "c" })]
    public void The_csv_reader_handles_quotes_commas_and_line_breaks(string text, string[] expected)
    {
        var reader = new StringReader(text);
        var all = new List<string>();
        for (var row = PincodeImporter.ReadRow(reader); row is not null; row = PincodeImporter.ReadRow(reader)) all.AddRange(row);
        Assert.Equal(expected, all);
    }

    [Fact]
    public async Task The_command_prints_a_summary_and_gives_clear_errors_for_a_bad_call_or_fileAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pincodes-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, Sample);
        try
        {
            await using var fixture = await MarketplaceHttpFixture.StartAsync();
            var output = new StringWriter();
            Assert.True(PincodeImportCommand.IsRequested(["import-pincodes", path]));
            Assert.False(PincodeImportCommand.IsRequested([]));
            Assert.False(PincodeImportCommand.IsRequested(["something-else"]));

            Assert.Equal(0, await PincodeImportCommand.RunAsync(fixture.Services, ["import-pincodes", path], output));
            Assert.Contains("Pincodes added: 6. Already known: 2.", output.ToString());   // 452001 and 141001 are in every database from the seed
            Assert.Contains("unknown state 'ATLANTIS'", output.ToString());

            var usage = new StringWriter();
            Assert.Equal(2, await PincodeImportCommand.RunAsync(fixture.Services, ["import-pincodes"], usage));
            Assert.Contains("Usage", usage.ToString());
            var missing = new StringWriter();
            Assert.Equal(2, await PincodeImportCommand.RunAsync(fixture.Services, ["import-pincodes", path + ".nope"], missing));
            Assert.Contains("File not found", missing.ToString());
        }
        finally { File.Delete(path); }
    }
}
