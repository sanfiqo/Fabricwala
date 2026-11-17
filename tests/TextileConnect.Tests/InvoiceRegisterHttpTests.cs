using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The register of tax documents for the accountant (IInvoiceRegister, docs/INVOICING.md).</summary>
public sealed class InvoiceRegisterHttpTests
{
    private const long RetailerId = 601, SellerAId = 611, SellerBId = 612;

    private static DateTime Utc(string text) => DateTime.Parse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapInvoiceRegisterEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Business Make(long id, BusinessType type, string name) => new() { Id = id, BusinessType = type, LegalName = name, DisplayName = name, Phone = $"+9190000{id:D5}", VerificationStatus = VerificationStatus.Approved };
        db.Businesses.AddRange(Make(RetailerId, BusinessType.Retailer, "Sharma Textiles"), Make(SellerAId, BusinessType.Manufacturer, "Kapoor Knits"), Make(SellerBId, BusinessType.Manufacturer, "Bedi Woollens"));
        await db.SaveChangesAsync();

        // Seller A: two tax invoices (one inside the state, one to another state), a credit note, and the platform fee invoice for the first order.
        var first = await OrderAsync(db, "TC-1", SellerAId);
        var second = await OrderAsync(db, "TC-2", SellerAId);
        var other = await OrderAsync(db, "TC-3", SellerBId);
        db.Invoices.AddRange(
            Doc(first, "11/2627/00001", "SellerToRetailer", "2026-11-17T10:00:00Z", 1000m, 30m, 25m, 25m, 0m, 1080m, "Sharma Textiles", "08AAAAA0000A1Z5", "08"),
            Doc(second, "11/2627/00002", "SellerToRetailer", "2026-11-18T10:00:00Z", 2000m, 0m, 0m, 0m, 100m, 2100m, "=HYPERLINK(\"http://evil.example\"),Shop", null, "07"),
            Doc(first, "C11/2627/00001", "CreditNote", "2026-11-20T10:00:00Z", 100m, 0m, 2.5m, 2.5m, 0m, 105m, "Sharma Textiles", "08AAAAA0000A1Z5", "08", refundId: 7),
            Doc(first, "PF/2627/00001", "PlatformCommission", "2026-11-17T11:00:00Z", 20m, 0m, 1.8m, 1.8m, 0m, 23.6m, "Kapoor Knits", "08AAAAA0000A1Z5", "08"),
            Doc(other, "12/2627/00001", "SellerToRetailer", "2026-11-17T12:00:00Z", 500m, 0m, 0m, 0m, 25m, 525m, "Sharma Textiles", null, "07"),
            Doc(first, "TC-20261117-AAAA", "PlatformCommission", "2026-11-17T13:00:00Z", null, null, null, null, null, null, null, null, null),      // the old statement: no figures, not in the register
            Doc(first, "11/2627/00000", "SellerToRetailer", "2026-10-31T18:00:00Z", 10m, 0m, 0.25m, 0.25m, 0m, 10.5m, "Old", null, "08"));            // 31 Oct 23:30 in India: in October, not in November
        await db.SaveChangesAsync();
        return fixture;
    }

    private static async Task<SellerOrder> OrderAsync(MarketplaceDbContext db, string number, long sellerId)
    {
        var order = new Order { OrderNumber = number, RetailerBusinessId = RetailerId, DeliveryLine1 = "x", DeliveryCity = "Indore", DeliveryPincode = "452001" };
        var sellerOrder = new SellerOrder { Order = order, ManufacturerBusinessId = sellerId, Status = SellerOrderStatus.Delivered };
        db.SellerOrders.Add(sellerOrder);
        await db.SaveChangesAsync();
        return sellerOrder;
    }

    private static Invoice Doc(SellerOrder order, string number, string type, string issued, decimal? taxable, decimal? shipping, decimal? cgst, decimal? sgst, decimal? igst, decimal? total,
        string? party, string? partyGstin, string? placeOfSupply, long refundId = 0)
        => new() { SellerOrderId = order.Id, InvoiceNumber = number, InvoiceType = type, RefundId = refundId, IssuedAt = Utc(issued), FileKey = "x", Taxable = taxable, Shipping = shipping, Cgst = cgst, Sgst = sgst,
            Igst = igst, Total = total, CounterpartyName = party, CounterpartyGstin = partyGstin, PlaceOfSupplyCode = placeOfSupply };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task A_seller_sees_their_sales_credit_notes_and_the_fee_invoices_they_received_with_totals()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);

        var page = await JsonAsync(await seller.GetAsync("/api/v1/seller/invoices/register?from=2026-11-01&to=2026-11-30"));

        Assert.Equal(4, page.GetProperty("totalRows").GetInt32());                         // two invoices, a credit note, a fee invoice; not the old statement, not the one of seller B, not October
        var rows = page.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(new[] { "11/2627/00001", "PF/2627/00001", "11/2627/00002", "C11/2627/00001" }, rows.Select(x => x.GetProperty("number").GetString()).ToArray());   // in order of date and time
        Assert.Equal("Purchase", rows[1].GetProperty("direction").GetString());
        Assert.Equal("Sale", rows[0].GetProperty("direction").GetString());
        Assert.Equal("2026-11-17", rows[0].GetProperty("date").GetString());

        var sales = page.GetProperty("sales");                                             // 1000 + 2000 - 100
        Assert.Equal(3, sales.GetProperty("documents").GetInt32());
        Assert.Equal(2900m, sales.GetProperty("taxable").GetDecimal());
        Assert.Equal(22.5m, sales.GetProperty("cgst").GetDecimal());                       // 25 - 2.5
        Assert.Equal(22.5m, sales.GetProperty("sgst").GetDecimal());
        Assert.Equal(100m, sales.GetProperty("igst").GetDecimal());
        Assert.Equal(3075m, sales.GetProperty("total").GetDecimal());                      // 1080 + 2100 - 105
        var purchases = page.GetProperty("purchases");
        Assert.Equal(1, purchases.GetProperty("documents").GetInt32());
        Assert.Equal(23.6m, purchases.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task The_dates_are_dates_in_india_and_the_default_is_this_month()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);

        var october = await JsonAsync(await seller.GetAsync("/api/v1/seller/invoices/register?from=2026-10-01&to=2026-10-31"));
        Assert.Equal("11/2627/00000", october.GetProperty("rows")[0].GetProperty("number").GetString());     // issued at 18:00 UTC, which is 23:30 on 31 October in India
        Assert.Equal(1, october.GetProperty("totalRows").GetInt32());
        Assert.Equal(1, (await JsonAsync(await seller.GetAsync("/api/v1/seller/invoices/register?from=2026-11-18&to=2026-11-18"))).GetProperty("totalRows").GetInt32());   // only the second invoice, issued on the 18th
        var dflt = await JsonAsync(await seller.GetAsync("/api/v1/seller/invoices/register"));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330)).Day, DateOnly.Parse(dflt.GetProperty("to").GetString()!).Day);
        Assert.Equal(1, DateOnly.Parse(dflt.GetProperty("from").GetString()!).Day);
    }

    [Fact]
    public async Task The_csv_has_one_line_per_document_credit_notes_in_minus_and_a_hostile_name_made_harmless()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);

        var response = await seller.GetAsync("/api/v1/seller/invoices/register?from=2026-11-01&to=2026-11-30&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("invoice-register-20261101-20261130.csv", response.Content.Headers.ContentDisposition!.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());          // Excel reads it as UTF-8
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Date,Number,Kind,Direction,Order,Party,PartyGstin,PlaceOfSupplyCode,Taxable,Shipping,CGST,SGST,IGST,Total", lines[0]);
        Assert.Equal(5, lines.Length);
        Assert.Equal("2026-11-17,11/2627/00001,TaxInvoice,Sale,TC-1,Sharma Textiles,08AAAAA0000A1Z5,08,1000.00,30.00,25.00,25.00,0.00,1080.00", lines[1]);
        Assert.Contains("C11/2627/00001,CreditNote,Sale,TC-1,Sharma Textiles,08AAAAA0000A1Z5,08,-100.00,0.00,-2.50,-2.50,0.00,-105.00", lines[4]);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil.example\"\"),Shop\"", lines[3]);     // not a formula, and the comma does not add a column
    }

    [Fact]
    public async Task The_csv_keeps_its_fourteen_columns_whatever_a_party_is_called()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);
        var text = Encoding.UTF8.GetString(await (await seller.GetAsync("/api/v1/seller/invoices/register?from=2026-11-01&to=2026-11-30&format=csv")).Content.ReadAsByteArrayAsync()).TrimStart('﻿');

        var rows = new List<string[]>();
        var reader = new StringReader(text);
        for (var row = TextileConnect.Infrastructure.Persistence.PincodeImporter.ReadRow(reader); row is not null; row = TextileConnect.Infrastructure.Persistence.PincodeImporter.ReadRow(reader)) rows.Add(row);

        Assert.Equal(5, rows.Count);
        Assert.All(rows, row => Assert.Equal(14, row.Length));
        Assert.StartsWith("'=", rows.Single(x => x[1] == "11/2627/00002")[5]);
    }

    [Fact]
    public async Task A_seller_never_sees_the_documents_of_another_seller()
    {
        await using var fixture = await StartAsync();
        using var sellerB = await fixture.ClientAsync("Manufacturer", "u-b", SellerBId);

        var page = await JsonAsync(await sellerB.GetAsync("/api/v1/seller/invoices/register?from=2026-11-01&to=2026-11-30"));

        Assert.Equal(1, page.GetProperty("totalRows").GetInt32());
        Assert.Equal("12/2627/00001", page.GetProperty("rows")[0].GetProperty("number").GetString());
    }

    [Fact]
    public async Task The_platform_register_holds_the_fee_invoices_of_every_seller_and_can_be_narrowed_to_one()
    {
        await using var fixture = await StartAsync();
        using var admin = await fixture.ClientAsync("Admin", "u-admin");

        var all = await JsonAsync(await admin.GetAsync("/api/v1/admin/invoices/register?from=2026-11-01&to=2026-11-30"));
        Assert.Equal(1, all.GetProperty("totalRows").GetInt32());
        var row = all.GetProperty("rows")[0];
        Assert.Equal("PF/2627/00001", row.GetProperty("number").GetString());
        Assert.Equal("Sale", row.GetProperty("direction").GetString());                    // the platform issued it
        Assert.Equal("Kapoor Knits", row.GetProperty("counterpartyName").GetString());
        Assert.Equal(23.6m, all.GetProperty("sales").GetProperty("total").GetDecimal());
        Assert.Equal(1, (await JsonAsync(await admin.GetAsync($"/api/v1/admin/invoices/register?from=2026-11-01&to=2026-11-30&sellerId={SellerAId}"))).GetProperty("totalRows").GetInt32());
        Assert.Equal(0, (await JsonAsync(await admin.GetAsync($"/api/v1/admin/invoices/register?from=2026-11-01&to=2026-11-30&sellerId={SellerBId}"))).GetProperty("totalRows").GetInt32());
        var csv = await admin.GetAsync("/api/v1/admin/invoices/register?from=2026-11-01&to=2026-11-30&format=csv");
        Assert.Equal("platform-fee-register-20261101-20261130.csv", csv.Content.Headers.ContentDisposition!.FileName);
    }

    [Theory]
    [InlineData("from=2026-13-01")]
    [InlineData("from=yesterday")]
    [InlineData("from=2026-11-30&to=2026-11-01")]
    [InlineData("from=2025-01-01&to=2026-11-30")]      // more than 366 days
    [InlineData("format=xml")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task A_wrong_query_is_a_bad_request(string query)
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);
        Assert.Equal(HttpStatusCode.BadRequest, (await seller.GetAsync("/api/v1/seller/invoices/register?" + query)).StatusCode);
    }

    [Fact]
    public async Task Only_the_right_roles_read_the_registers()
    {
        await using var fixture = await StartAsync();
        using var retailer = await fixture.ClientAsync("Retailer", "u-r", RetailerId);
        using var seller = await fixture.ClientAsync("Manufacturer", "u-a", SellerAId);
        using var anonymous = await fixture.ClientAsync(null);

        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/seller/invoices/register")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/admin/invoices/register")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/invoices/register")).StatusCode);
    }
}
