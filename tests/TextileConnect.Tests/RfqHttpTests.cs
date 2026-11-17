using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Rfq;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Rfq;
using TextileConnect.Infrastructure.Persistence;
using TextileConnect.Infrastructure.Rfq;

namespace TextileConnect.Tests;

/// <summary>Requests for quotation: a retailer asks, manufacturers quote, the retailer accepts one (docs/RFQ.md).</summary>
public sealed class RfqHttpTests
{
    private const long RetailerId = 501, OtherRetailerId = 502, SellerAId = 511, SellerBId = 512;

    private sealed record World(MarketplaceHttpFixture Fixture, HttpClient Retailer, HttpClient OtherRetailer, HttpClient SellerA, HttpClient SellerB, HttpClient Admin, long CategoryId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var client in new[] { Retailer, OtherRetailer, SellerA, SellerB, Admin }) client.Dispose();
            await Fixture.DisposeAsync();
        }
    }

    private static async Task<World> StartAsync(bool featureOn = true)
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => { app.MapFeatureEndpoints(); app.MapRfqEndpoints(); app.MapAdminEndpoints(); });
        long categoryId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            if (!await db.PostalCodes.AnyAsync(x => x.Pincode == "452001"))                 // a real database has it from the master data, the in-memory one does not
            {
                var state = new State { Name = "Madhya Pradesh", Code = "MP" }; db.States.Add(state); await db.SaveChangesAsync();
                var city = new City { Name = "Indore", StateId = state.Id }; db.Cities.Add(city); await db.SaveChangesAsync();
                db.PostalCodes.Add(new PostalCode { Pincode = "452001", CityId = city.Id });
            }
            var category = await db.Categories.OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new Category { Name = "Winter wear", Slug = "winter-wear" };   // a real database already has the seeded ones
            if (category.Id == 0) db.Categories.Add(category);
            Business Make(long id, BusinessType type, string name, string phone) => new() { Id = id, BusinessType = type, LegalName = name, DisplayName = name, Phone = phone, VerificationStatus = VerificationStatus.Approved };
            db.Businesses.AddRange(Make(RetailerId, BusinessType.Retailer, "Sharma Textiles", "+919000000501"), Make(OtherRetailerId, BusinessType.Retailer, "Other Shop", "+919000000502"),
                Make(SellerAId, BusinessType.Manufacturer, "Kapoor Knits", "+919000000511"), Make(SellerBId, BusinessType.Manufacturer, "Bedi Woollens", "+919000000512"));
            await db.SaveChangesAsync();
            foreach (var (business, user) in new[] { (RetailerId, "u-retailer"), (OtherRetailerId, "u-other"), (SellerAId, "u-seller-a"), (SellerBId, "u-seller-b") })
                db.BusinessUsers.Add(new BusinessUser { BusinessId = business, UserId = user, Role = BusinessMemberRole.Owner, IsPrimary = true });
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }
        var admin = await fixture.ClientAsync("Admin", "u-admin");
        if (featureOn) Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/admin/features/Rfq", new { on = true })).StatusCode);
        return new World(fixture, await fixture.ClientAsync("Retailer", "u-retailer", RetailerId), await fixture.ClientAsync("Retailer", "u-other", OtherRetailerId),
            await fixture.ClientAsync("Manufacturer", "u-seller-a", SellerAId), await fixture.ClientAsync("Manufacturer", "u-seller-b", SellerBId), admin, categoryId);
    }

    private static object Body(long? categoryId = null, string title = "500 school sweaters in navy", int quantity = 500, string pincode = "452001", decimal? target = 380m)
        => new { title, description = "Round neck, full sleeves, sizes 28 to 40.", categoryId, quantity, unit = "Piece", targetPricePerUnit = target, deliveryPincode = pincode, expiresInDays = 14 };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<long> CreateAsync(HttpClient retailer, long? categoryId = null, string title = "500 school sweaters in navy")
    {
        var response = await retailer.PostAsJsonAsync("/api/v1/rfqs", Body(categoryId, title));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("summary").GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> QuoteAsync(HttpClient seller, long rfqId, decimal price = 350m, int quantity = 500, int days = 21, string? note = "Ready in three weeks.")
        => seller.PutAsJsonAsync($"/api/v1/seller/rfqs/{rfqId}/quotation", new { pricePerUnit = price, quantity, leadTimeDays = days, note });

    // ---- the switch -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Everything_answers_404_feature_off_until_the_owner_switches_it_on()
    {
        await using var world = await StartAsync(featureOn: false);

        var off = await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", Body());
        Assert.Equal(HttpStatusCode.NotFound, off.StatusCode);
        Assert.Equal("feature_off", (await JsonAsync(off)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await world.SellerA.GetAsync("/api/v1/seller/rfqs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.GetAsync("/api/v1/admin/rfqs")).StatusCode);
        Assert.False((await JsonAsync(await world.Retailer.GetAsync("/api/v1/features"))).GetProperty("Rfq").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Retailer.PutAsJsonAsync("/api/v1/admin/features/Rfq", new { on = true })).StatusCode);   // only an admin switches it
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PutAsJsonAsync("/api/v1/admin/features/Nothing", new { on = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Admin.PutAsJsonAsync("/api/v1/admin/features/Rfq", new { on = true })).StatusCode);
        Assert.True((await JsonAsync(await world.Retailer.GetAsync("/api/v1/features"))).GetProperty("Rfq").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", Body())).StatusCode);
    }

    [Fact]
    public async Task Only_a_retailer_asks_and_only_a_manufacturer_quotes_and_nobody_without_a_session_does_either()
    {
        await using var world = await StartAsync();
        using var anonymous = await world.Fixture.ClientAsync(null);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.SellerA.PostAsJsonAsync("/api/v1/rfqs", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Retailer.GetAsync("/api/v1/seller/rfqs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Retailer.PostAsJsonAsync("/api/v1/admin/rfqs/1/close", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/rfqs", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/rfqs")).StatusCode);
    }

    // ---- asking -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_request_is_checked_and_a_wrong_field_is_named()
    {
        await using var world = await StartAsync();

        var bad = await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", new { title = "hi", quantity = 0, unit = "Bundle", targetPricePerUnit = -5, deliveryPincode = "452001", expiresInDays = 90, categoryId = 99999 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var error = await JsonAsync(bad);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        foreach (var field in new[] { "title", "quantity", "unit", "targetPricePerUnit", "expiresInDays", "categoryId" })
            Assert.True(error.GetProperty("fields").TryGetProperty(field, out var messages) && messages.GetArrayLength() == 1, field);

        var unknown = await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", Body(pincode: "999999"));
        Assert.Equal("pincode_unknown", (await JsonAsync(unknown)).GetProperty("code").GetString());
        var invalid = await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", Body(pincode: "12"));
        Assert.Equal("pincode_invalid", (await JsonAsync(invalid)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_retailer_can_have_ten_open_requests_and_the_eleventh_says_why_not()
    {
        await using var world = await StartAsync();
        for (var i = 0; i < EfRfqService.MaxOpenPerRetailer; i++) await CreateAsync(world.Retailer, title: $"Request number {i} for sweaters");

        var eleventh = await world.Retailer.PostAsJsonAsync("/api/v1/rfqs", Body());

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal("rfq_limit_reached", (await JsonAsync(eleventh)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await world.OtherRetailer.PostAsJsonAsync("/api/v1/rfqs", Body())).StatusCode);     // the limit is per business
    }

    // ---- the whole story --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_request_is_quoted_by_two_manufacturers_and_one_quotation_is_accepted()
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer, world.CategoryId);

        // A manufacturer finds it in the feed. The buyer is not named: only the city and the state.
        var feed = await JsonAsync(await world.SellerA.GetAsync("/api/v1/seller/rfqs"));
        Assert.Equal(1, feed.GetProperty("total").GetInt32());
        var row = feed.GetProperty("items")[0];
        Assert.Equal(id, row.GetProperty("id").GetInt64());
        Assert.Equal("Indore", row.GetProperty("deliveryCity").GetString());
        Assert.False(row.GetProperty("iHaveQuoted").GetBoolean());
        var detailForSeller = await JsonAsync(await world.SellerA.GetAsync($"/api/v1/seller/rfqs/{id}"));
        Assert.Equal(JsonValueKind.Null, detailForSeller.GetProperty("buyerContact").ValueKind);
        Assert.DoesNotContain("Sharma", detailForSeller.ToString());

        // Two quotations.
        var a = await JsonAsync(await QuoteAsync(world.SellerA, id, 350m, 500, 21));
        Assert.Equal("Submitted", a.GetProperty("quotations")[0].GetProperty("status").GetString());
        Assert.Equal(175000m, a.GetProperty("quotations")[0].GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await QuoteAsync(world.SellerB, id, 340m, 400, 30)).StatusCode);
        Assert.True((await JsonAsync(await world.SellerA.GetAsync("/api/v1/seller/rfqs"))).GetProperty("items")[0].GetProperty("iHaveQuoted").GetBoolean());

        // A manufacturer sees only their own quotation; the buyer sees both, cheapest first, with names.
        Assert.Equal(1, (await JsonAsync(await world.SellerB.GetAsync($"/api/v1/seller/rfqs/{id}"))).GetProperty("quotations").GetArrayLength());
        var buyerView = await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"));
        var quotations = buyerView.GetProperty("quotations");
        Assert.Equal(2, quotations.GetArrayLength());
        Assert.Equal("Bedi Woollens", quotations[0].GetProperty("sellerName").GetString());
        Assert.Equal(2, buyerView.GetProperty("summary").GetProperty("quotationCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, quotations[0].GetProperty("contact").ValueKind);                 // no contact before a decision

        // The buyer was told about each quotation.
        using (var scope = world.Fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == "u-retailer" && x.Title == "New quotation"));
        }

        // Accept the one of Kapoor Knits.
        var quotationOfA = quotations.EnumerateArray().Single(x => x.GetProperty("sellerName").GetString() == "Kapoor Knits").GetProperty("id").GetInt64();
        var accepted = await world.Retailer.PostAsync($"/api/v1/rfqs/{id}/quotations/{quotationOfA}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var after = await JsonAsync(accepted);
        Assert.Equal("Awarded", after.GetProperty("summary").GetProperty("status").GetString());
        var winner = after.GetProperty("quotations").EnumerateArray().Single(x => x.GetProperty("status").GetString() == "Accepted");
        Assert.Equal("+919000000511", winner.GetProperty("contact").GetProperty("phone").GetString());      // the buyer learns who to call
        Assert.Equal("Rejected", after.GetProperty("quotations").EnumerateArray().Single(x => x.GetProperty("sellerName").GetString() == "Bedi Woollens").GetProperty("status").GetString());

        // The winner learns who the buyer is; the other manufacturer does not.
        var winnerView = await JsonAsync(await world.SellerA.GetAsync($"/api/v1/seller/rfqs/{id}"));
        Assert.Equal("Sharma Textiles", winnerView.GetProperty("buyerContact").GetProperty("name").GetString());
        Assert.Equal("+919000000501", winnerView.GetProperty("buyerContact").GetProperty("phone").GetString());
        var loserView = await JsonAsync(await world.SellerB.GetAsync($"/api/v1/seller/rfqs/{id}"));
        Assert.Equal(JsonValueKind.Null, loserView.GetProperty("buyerContact").ValueKind);
        Assert.Equal(0, (await JsonAsync(await world.SellerB.GetAsync("/api/v1/seller/rfqs"))).GetProperty("total").GetInt32());             // no longer open

        using (var scope = world.Fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "u-seller-a" && x.Title == "Your quotation was accepted"));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "u-seller-b" && x.Title == "Another quotation was chosen"));
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "rfq.awarded"));
        }

        // It is final.
        Assert.Equal("rfq_not_open", (await JsonAsync(await world.Retailer.PostAsync($"/api/v1/rfqs/{id}/cancel", null))).GetProperty("code").GetString());
        Assert.Equal("rfq_not_open", (await JsonAsync(await QuoteAsync(world.SellerB, id))).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_quotation_can_be_changed_withdrawn_and_sent_again_until_it_is_decided()
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);

        await QuoteAsync(world.SellerA, id, 350m);
        var changed = await JsonAsync(await QuoteAsync(world.SellerA, id, 330m, 300, 10, "Better price"));
        Assert.Equal(1, changed.GetProperty("quotations").GetArrayLength());                               // one quotation per manufacturer
        Assert.Equal(330m, changed.GetProperty("quotations")[0].GetProperty("pricePerUnit").GetDecimal());

        var withdrawn = await JsonAsync(await world.SellerA.DeleteAsync($"/api/v1/seller/rfqs/{id}/quotation"));
        Assert.Equal("Withdrawn", withdrawn.GetProperty("quotations")[0].GetProperty("status").GetString());
        Assert.Equal(0, (await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"))).GetProperty("quotations").GetArrayLength());   // the buyer no longer sees it
        Assert.Equal("quotation_final", (await JsonAsync(await world.SellerA.DeleteAsync($"/api/v1/seller/rfqs/{id}/quotation"))).GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.OK, (await QuoteAsync(world.SellerA, id, 320m)).StatusCode);          // sent again
        var mine = await JsonAsync(await world.SellerA.GetAsync("/api/v1/seller/quotations"));
        Assert.Equal(1, mine.GetProperty("total").GetInt32());
        Assert.Equal("Submitted", mine.GetProperty("items")[0].GetProperty("quotation").GetProperty("status").GetString());
        Assert.Equal(id, mine.GetProperty("items")[0].GetProperty("rfq").GetProperty("id").GetInt64());

        var quotationId = (await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"))).GetProperty("quotations")[0].GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await world.Retailer.PostAsync($"/api/v1/rfqs/{id}/quotations/{quotationId}/accept", null)).StatusCode);
        Assert.Equal("rfq_not_open", (await JsonAsync(await QuoteAsync(world.SellerA, id, 300m))).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(0, 500, 10, "pricePerUnit")]
    [InlineData(350, 0, 10, "quantity")]
    [InlineData(350, 501, 10, "quantity")]       // more than was asked
    [InlineData(350, 500, 0, "leadTimeDays")]
    [InlineData(350, 500, 366, "leadTimeDays")]
    public async Task A_quotation_is_checked_and_a_wrong_field_is_named(decimal price, int quantity, int days, string field)
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);

        var response = await QuoteAsync(world.SellerA, id, price, quantity, days);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("fields").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Cancelling_ends_the_request_and_tells_the_manufacturers_who_quoted()
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);
        await QuoteAsync(world.SellerA, id);

        var cancelled = await JsonAsync(await world.Retailer.PostAsync($"/api/v1/rfqs/{id}/cancel", null));

        Assert.Equal("Cancelled", cancelled.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(0, (await JsonAsync(await world.SellerB.GetAsync("/api/v1/seller/rfqs"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await world.SellerB.GetAsync($"/api/v1/seller/rfqs/{id}")).StatusCode);        // B never quoted: a closed request is not theirs to see
        Assert.Equal(HttpStatusCode.OK, (await world.SellerA.GetAsync($"/api/v1/seller/rfqs/{id}")).StatusCode);              // A did: their history stays
        using var scope = world.Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "u-seller-a" && x.Title == "Request cancelled"));
        Assert.Equal(0, await db.Notifications.CountAsync(x => x.UserId == "u-seller-b"));
    }

    // ---- who may see what -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Somebody_elses_request_is_not_found_whatever_is_tried()
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);
        await QuoteAsync(world.SellerA, id);
        var quotationId = (await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"))).GetProperty("quotations")[0].GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await world.OtherRetailer.GetAsync($"/api/v1/rfqs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.OtherRetailer.PostAsync($"/api/v1/rfqs/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.OtherRetailer.PostAsync($"/api/v1/rfqs/{id}/quotations/{quotationId}/accept", null)).StatusCode);
        Assert.Equal(0, (await JsonAsync(await world.OtherRetailer.GetAsync("/api/v1/rfqs"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await world.SellerB.DeleteAsync($"/api/v1/seller/rfqs/{id}/quotation")).StatusCode);     // B has nothing to withdraw
        Assert.Equal(HttpStatusCode.NotFound, (await world.Retailer.PostAsync($"/api/v1/rfqs/{id}/quotations/99999/accept", null)).StatusCode);
        Assert.Equal(1, (await JsonAsync(await world.Retailer.GetAsync("/api/v1/rfqs"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await world.Retailer.GetAsync($"/api/v1/rfqs/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_feed_can_be_filtered_by_category_and_words()
    {
        await using var world = await StartAsync();
        await CreateAsync(world.Retailer, world.CategoryId, "Navy school sweaters");
        await CreateAsync(world.Retailer, null, "White cotton shirting fabric");

        Assert.Equal(2, (await JsonAsync(await world.SellerA.GetAsync("/api/v1/seller/rfqs"))).GetProperty("total").GetInt32());
        Assert.Equal(1, (await JsonAsync(await world.SellerA.GetAsync($"/api/v1/seller/rfqs?categoryId={world.CategoryId}"))).GetProperty("total").GetInt32());
        Assert.Equal("White cotton shirting fabric", (await JsonAsync(await world.SellerA.GetAsync("/api/v1/seller/rfqs?q=SHIRTING"))).GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await world.SellerA.GetAsync("/api/v1/seller/rfqs?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Retailer.GetAsync("/api/v1/rfqs?page=0")).StatusCode);
    }

    [Fact]
    public async Task The_admin_lists_and_closes_requests_and_everyone_involved_is_told()
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);
        await QuoteAsync(world.SellerA, id);

        var list = await JsonAsync(await world.Admin.GetAsync("/api/v1/admin/rfqs"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal("Sharma Textiles", list.GetProperty("items")[0].GetProperty("retailerName").GetString());
        Assert.Equal(1, list.GetProperty("items")[0].GetProperty("quotationCount").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await world.Admin.PostAsJsonAsync($"/api/v1/admin/rfqs/{id}/close", new { reason = "Not a textile request" })).StatusCode);
        Assert.Equal("Closed", (await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"))).GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await world.Admin.PostAsJsonAsync($"/api/v1/admin/rfqs/{id}/close", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PostAsJsonAsync("/api/v1/admin/rfqs/99999/close", new { })).StatusCode);
        Assert.Equal(1, (await JsonAsync(await world.Admin.GetAsync("/api/v1/admin/rfqs?status=Closed"))).GetProperty("total").GetInt32());
        using var scope = world.Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "u-retailer" && x.Title == "Request closed"));
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "u-seller-a" && x.Title == "Request closed"));
        Assert.Equal("Not a textile request", (await db.AuditLogs.SingleAsync(x => x.Action == "rfq.closed")).Details);
    }

    // ---- time -------------------------------------------------------------------------------------------------------------------

    private sealed class MovingClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task A_request_expires_by_itself_and_then_cannot_be_quoted_or_accepted()
    {
        var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var state = new State { Name = "Madhya Pradesh", Code = "MP" }; db.States.Add(state); await db.SaveChangesAsync();
        var city = new City { Name = "Indore", StateId = state.Id }; db.Cities.Add(city); await db.SaveChangesAsync();
        db.PostalCodes.Add(new PostalCode { Pincode = "452001", CityId = city.Id });
        db.Businesses.AddRange(new Business { Id = 1, BusinessType = BusinessType.Retailer, LegalName = "R", DisplayName = "R", Phone = "+919000000001" },
            new Business { Id = 2, BusinessType = BusinessType.Manufacturer, LegalName = "M", DisplayName = "M", Phone = "+919000000002" });
        await db.SaveChangesAsync();
        var clock = new MovingClock(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
        var service = new EfRfqService(db, null, clock);
        var created = await service.CreateAsync(1, new RfqInput("Hundred woollen shawls", null, null, 100, null, null, "452001", null, 7));
        var rfqId = created.Summary.Id;
        var quotation = (await service.QuoteAsync(2, rfqId, new QuotationInput(500m, 100, 10, null)))!.Quotations.Single();

        clock.Now = clock.Now.AddDays(6);
        Assert.Equal("Open", (await service.GetMineAsync(1, rfqId))!.Summary.Status);
        Assert.Single((await service.FeedAsync(2, null, null, 1, 20)).Items);

        clock.Now = clock.Now.AddDays(2);                                          // day 8: past the 7 days
        Assert.Equal("Expired", (await service.GetMineAsync(1, rfqId))!.Summary.Status);
        Assert.Empty((await service.FeedAsync(2, null, null, 1, 20)).Items);
        Assert.Equal("rfq_not_open", (await Assert.ThrowsAsync<TextileConnect.Application.Common.RuleViolationException>(() => service.QuoteAsync(2, rfqId, new QuotationInput(480m, 100, 10, null)))).Code);
        Assert.Equal("rfq_not_open", (await Assert.ThrowsAsync<TextileConnect.Application.Common.RuleViolationException>(() => service.AcceptAsync(1, rfqId, quotation.Id))).Code);
        Assert.Equal("rfq_not_open", (await Assert.ThrowsAsync<TextileConnect.Application.Common.RuleViolationException>(() => service.CancelAsync(1, rfqId))).Code);
        Assert.Equal(1, (await service.ListMineAsync(1, "Expired", 1, 20)).Total);
        Assert.Equal(0, (await service.ListMineAsync(1, "Open", 1, 20)).Total);
        Assert.NotNull(await service.GetForSellerAsync(2, rfqId));                 // the manufacturer who quoted keeps seeing it
    }

    [Fact]
    public async Task An_expired_request_no_longer_counts_toward_the_limit_of_open_ones()
    {
        var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var state = new State { Name = "Madhya Pradesh", Code = "MP" }; db.States.Add(state); await db.SaveChangesAsync();
        var city = new City { Name = "Indore", StateId = state.Id }; db.Cities.Add(city); await db.SaveChangesAsync();
        db.PostalCodes.Add(new PostalCode { Pincode = "452001", CityId = city.Id });
        db.Businesses.Add(new Business { Id = 1, BusinessType = BusinessType.Retailer, LegalName = "R", DisplayName = "R", Phone = "+919000000001" });
        await db.SaveChangesAsync();
        var clock = new MovingClock(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
        var service = new EfRfqService(db, null, clock);
        for (var i = 0; i < EfRfqService.MaxOpenPerRetailer; i++) await service.CreateAsync(1, new RfqInput($"Request number {i} for shawls", null, null, 10, null, null, "452001", null, 1));
        await Assert.ThrowsAsync<TextileConnect.Application.Common.RuleViolationException>(() => service.CreateAsync(1, new RfqInput("One more request", null, null, 10, null, null, "452001", null, 1)));

        clock.Now = clock.Now.AddDays(2);

        Assert.NotNull(await service.CreateAsync(1, new RfqInput("One more request", null, null, 10, null, null, "452001", null, 1)));
    }

    [MySqlFact]
    public Task Two_accepts_at_the_same_moment_award_one_quotation_only() => MySqlStore.RunAsync(async () =>
    {
        await using var world = await StartAsync();
        var id = await CreateAsync(world.Retailer);
        await QuoteAsync(world.SellerA, id, 350m);
        await QuoteAsync(world.SellerB, id, 340m);
        var quotations = (await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"))).GetProperty("quotations").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

        var results = await Task.WhenAll(quotations.Select(q => world.Retailer.PostAsync($"/api/v1/rfqs/{id}/quotations/{q}/accept", null)));

        Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        var final = await JsonAsync(await world.Retailer.GetAsync($"/api/v1/rfqs/{id}"));
        Assert.Equal(1, final.GetProperty("quotations").EnumerateArray().Count(x => x.GetProperty("status").GetString() == "Accepted"));
    });
}
