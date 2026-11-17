using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Application.Invoicing;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class InvoiceTests
{
    // ---- the pure rules -------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "Rupees Zero Only")]
    [InlineData(1, "Rupees One Only")]
    [InlineData(19, "Rupees Nineteen Only")]
    [InlineData(100, "Rupees One Hundred Only")]
    [InlineData(1050.05, "Rupees One Thousand Fifty and Five Paise Only")]
    [InlineData(123456.5, "Rupees One Lakh Twenty Three Thousand Four Hundred Fifty Six and Fifty Paise Only")]
    [InlineData(10000000, "Rupees One Crore Only")]
    [InlineData(1000000000, "Rupees One Hundred Crore Only")]
    public void Amounts_are_written_in_lakh_and_crore(double amount, string words) => Assert.Equal(words, IndianCurrencyWords.Of((decimal)amount));

    [Theory]
    [InlineData("2026-04-01T00:00:00Z", "2627")]
    [InlineData("2027-03-31T18:29:00Z", "2627")]   // 23:59 on 31 March in India
    [InlineData("2027-03-31T18:30:00Z", "2728")]   // midnight: 1 April in India
    [InlineData("2026-01-15T10:00:00Z", "2526")]
    [InlineData("2026-03-31T20:00:00Z", "2627")]   // already 1 April 01:30 in India, although it is still 31 March in UTC
    public void The_financial_year_is_judged_by_the_date_in_India(string utc, string expected)
        => Assert.Equal(expected, FinancialYear.Code(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Fact]
    public void An_invoice_number_has_at_most_sixteen_characters()
    {
        Assert.Equal("12/2627/00001", InvoiceNumbers.Format(12, "2627", 1));
        Assert.Equal(16, InvoiceNumbers.Format(99999, "2627", 99999).Length);
        Assert.Throws<InvalidOperationException>(() => InvoiceNumbers.Format(1234567, "2627", 1));
    }

    [Fact]
    public void Every_state_of_the_master_data_has_a_gst_code_and_the_codes_are_distinct()
    {
        Assert.All(IndiaMasterData.States, state => Assert.NotNull(GstStateCodes.For(state.Code)));
        var codes = IndiaMasterData.States.Select(x => GstStateCodes.For(x.Code)).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal("08", GstStateCodes.For("RJ"));
        Assert.Equal("07", GstStateCodes.For("DL"));
        Assert.Null(GstStateCodes.For("XX"));
    }

    private static GstInvoiceInput Input(string sellerState, string supplyState, decimal shipping = 100m)
        => new("12/2627/00001", DateTime.Parse("2026-11-17T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            new GstParty("Seller Mills", "08AAAAA0000A1Z5", "1 Mill Road, Jaipur, 302001", "Rajasthan", sellerState), new GstParty("Retail Shop", null, "5 Market, Delhi", "Delhi", supplyState),
            "5 Market, Delhi, 110001", "Delhi", supplyState, "TC-1",
            [new GstItemInput("Cotton shirt", "M", "Blue", "6205", 10, 250m, 5m, 125m), new GstItemInput("Silk saree", null, null, null, 2, 1000m, 12m, 240m)],
            shipping, 2500m + 2000m + shipping + 365m);

    [Fact]
    public void A_sale_inside_the_sellers_state_is_split_in_cgst_and_sgst()
    {
        var invoice = GstInvoiceBuilder.Build(Input("08", "08"));
        Assert.False(invoice.InterState);
        Assert.Equal(4500m, invoice.Taxable);
        Assert.Equal(365m, invoice.Tax);
        Assert.Equal(182.5m, invoice.Cgst);
        Assert.Equal(182.5m, invoice.Sgst);
        Assert.Equal(0m, invoice.Igst);
        Assert.Equal(4965m, invoice.Total);
        Assert.Equal(0m, invoice.RoundOff);
    }

    [Fact]
    public void A_sale_to_another_state_is_igst_only()
    {
        var invoice = GstInvoiceBuilder.Build(Input("08", "07"));
        Assert.True(invoice.InterState);
        Assert.Equal(365m, invoice.Igst);
        Assert.Equal(0m, invoice.Cgst + invoice.Sgst);
    }

    [Fact]
    public void An_odd_paisa_of_tax_never_goes_missing_in_the_split()
    {
        var input = Input("08", "08") with { Items = [new GstItemInput("Cap", null, null, "6505", 1, 10.01m, 5m, 0.50m)], Total = 10.51m + 100m };
        var invoice = GstInvoiceBuilder.Build(input);
        Assert.Equal(0.50m, invoice.Cgst + invoice.Sgst);
    }

    [Fact]
    public void A_total_that_does_not_match_the_lines_shows_as_round_off_instead_of_hiding()
    {
        var invoice = GstInvoiceBuilder.Build(Input("08", "08") with { Total = 4965.40m });
        Assert.Equal(0.40m, invoice.RoundOff);
        Assert.Equal("Rupees Four Thousand Nine Hundred Sixty Five and Forty Paise Only", invoice.AmountInWords);
    }

    [Fact]
    public void The_pdf_is_a_real_pdf_with_the_invoice_drawn_on_it()
    {
        var pdf = GstInvoicePdf.Render(GstInvoiceBuilder.Build(Input("08", "08")));
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 2000);
        if (Environment.GetEnvironmentVariable("INVOICE_SAMPLE_DIR") is { Length: > 0 } dir)
        {
            File.WriteAllBytes(Path.Combine(dir, "invoice-intra.pdf"), pdf);
            File.WriteAllBytes(Path.Combine(dir, "invoice-inter.png"), GstInvoicePdf.RenderImages(GstInvoiceBuilder.Build(Input("08", "07"))).First());
        }
    }

    // ---- the service, on a small database -----------------------------------------------------------------------------------------

    private sealed class MemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public async Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream(); await content.CopyToAsync(copy, cancellationToken);
            var key = Guid.NewGuid().ToString("N") + "-" + fileName; Files[key] = copy.ToArray(); return key;
        }
        public Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(Files[fileKey]));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private static string Gstin(string stateCode)
    {
        var body = stateCode + "AAAAA0000A1Z";
        return body + GstinRules.CheckCharacter(body + "0");
    }

    private sealed class World
    {
        public required MarketplaceDbContext Db { get; init; }
        public required MemoryStorage Storage { get; init; }
        public required Business Seller { get; init; }
        public required Business Retailer { get; init; }

        public EfFinanceService Service(DateTimeOffset? at = null) => new(Db, Storage, new FixedClock(at ?? new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero)));

        public async Task<SellerOrder> OrderAsync(string status = SellerOrderStatus.Dispatched, string pincode = "302001", long? sellerId = null, decimal commissionGst = 0m)
        {
            var order = new Order { OrderNumber = $"TC-{Guid.NewGuid():N}"[..14], RetailerBusinessId = Retailer.Id, DeliveryLine1 = "5 Market", DeliveryCity = "Jaipur", DeliveryPincode = pincode };
            var sellerOrder = new SellerOrder { Order = order, ManufacturerBusinessId = sellerId ?? Seller.Id, Status = status, SubTotal = 1000m, TaxAmount = 50m, ShippingAmount = 30m, Total = 1080m, CommissionRate = 0.02m, CommissionAmount = 20m, CommissionGst = commissionGst };
            sellerOrder.Items.Add(new OrderItem { ProductId = 1, Title = "Shirt", Size = "M", Quantity = 10, UnitPrice = 100m, GstRate = 5m, GstAmount = 50m, HsnCode = "6205" });
            Db.SellerOrders.Add(sellerOrder);
            await Db.SaveChangesAsync();
            return sellerOrder;
        }
    }

    private static async Task<World> WorldAsync(bool sellerHasGstin = true, bool sellerHasAddress = true)
    {
        var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rajasthan = new State { Name = "Rajasthan", Code = "RJ" }; var delhi = new State { Name = "Delhi", Code = "DL" };
        db.States.AddRange(rajasthan, delhi);
        await db.SaveChangesAsync();
        var jaipur = new City { Name = "Jaipur", StateId = rajasthan.Id }; var newDelhi = new City { Name = "New Delhi", StateId = delhi.Id };
        db.Cities.AddRange(jaipur, newDelhi);
        await db.SaveChangesAsync();
        db.PostalCodes.AddRange(new PostalCode { Pincode = "302001", CityId = jaipur.Id }, new PostalCode { Pincode = "110001", CityId = newDelhi.Id });
        var seller = new Business { BusinessType = BusinessType.Manufacturer, LegalName = "Seller Mills", DisplayName = "Seller", Phone = "+919999900001", Gstin = sellerHasGstin ? Gstin("08") : null };
        var retailer = new Business { BusinessType = BusinessType.Retailer, LegalName = "Retail Shop", DisplayName = "Shop", Phone = "+919999900002" };
        db.Businesses.AddRange(seller, retailer);
        await db.SaveChangesAsync();
        if (sellerHasAddress) db.BusinessAddresses.Add(new BusinessAddress { BusinessId = seller.Id, AddressType = AddressType.Registered, Line1 = "1 Mill Road", CityId = jaipur.Id, Pincode = "302001", IsDefault = true });
        await db.SaveChangesAsync();
        return new World { Db = db, Storage = new MemoryStorage(), Seller = seller, Retailer = retailer };
    }

    [Fact]
    public async Task The_first_invoice_of_a_seller_in_a_year_is_number_one_and_the_next_is_two()
    {
        var world = await WorldAsync();
        var first = await world.Service().CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer");
        var second = await world.Service().CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer");

        Assert.Equal($"{world.Seller.Id}/2627/00001", first.InvoiceNumber);
        Assert.Equal($"{world.Seller.Id}/2627/00002", second.InvoiceNumber);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(world.Storage.Files[first.FileKey!], 0, 5));
    }

    [Fact]
    public async Task A_new_financial_year_starts_the_series_again_and_another_seller_has_its_own_series()
    {
        var world = await WorldAsync();
        var other = new Business { BusinessType = BusinessType.Manufacturer, LegalName = "Other Mills", DisplayName = "Other", Phone = "+919999900003", Gstin = Gstin("08") };
        world.Db.Businesses.Add(other); await world.Db.SaveChangesAsync();
        world.Db.BusinessAddresses.Add(new BusinessAddress { BusinessId = other.Id, AddressType = AddressType.Registered, Line1 = "2 Road", CityId = world.Db.Cities.First().Id, Pincode = "302001" });
        await world.Db.SaveChangesAsync();

        var march = await world.Service(new DateTimeOffset(2027, 3, 31, 12, 0, 0, TimeSpan.Zero)).CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer");
        var april = await world.Service(new DateTimeOffset(2027, 4, 2, 12, 0, 0, TimeSpan.Zero)).CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer");
        var otherSeller = await world.Service().CreateInvoiceAsync((await world.OrderAsync(sellerId: other.Id)).Id, "SellerToRetailer");

        Assert.EndsWith("/2627/00001", march.InvoiceNumber);
        Assert.EndsWith("/2728/00001", april.InvoiceNumber);
        Assert.Equal($"{other.Id}/2627/00001", otherSeller.InvoiceNumber);
    }

    [Fact]
    public async Task Asking_again_returns_the_same_document_and_uses_no_new_number()
    {
        var world = await WorldAsync();
        var sellerOrder = await world.OrderAsync();
        var first = await world.Service().CreateInvoiceAsync(sellerOrder.Id, "SellerToRetailer");
        var again = await world.Service().CreateInvoiceAsync(sellerOrder.Id, "SellerToRetailer");

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await world.Db.Invoices.CountAsync());
        Assert.Equal(1, (await world.Db.InvoiceSequences.SingleAsync()).LastNumber);
    }

    [Theory]
    [InlineData(SellerOrderStatus.Pending)]
    [InlineData(SellerOrderStatus.Accepted)]
    [InlineData(SellerOrderStatus.Rejected)]
    [InlineData(SellerOrderStatus.Cancelled)]
    public async Task No_invoice_is_made_before_the_goods_are_dispatched(string status)
    {
        var world = await WorldAsync();
        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync(status)).Id, "SellerToRetailer"));
        Assert.Equal("invoice_not_ready", error.Code);
        Assert.Empty(world.Db.Invoices);
        Assert.Empty(world.Db.InvoiceSequences);
    }

    [Fact]
    public async Task A_seller_without_a_valid_gstin_gets_a_clear_error_and_no_number_is_used()
    {
        var world = await WorldAsync(sellerHasGstin: false);
        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer"));
        Assert.Equal("seller_gstin_missing", error.Code);
        Assert.Empty(world.Db.InvoiceSequences);
        Assert.Empty(world.Storage.Files);
    }

    [Fact]
    public async Task A_seller_without_a_street_address_gets_a_clear_error()
    {
        var world = await WorldAsync(sellerHasAddress: false);
        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync()).Id, "SellerToRetailer"));
        Assert.Equal("seller_address_missing", error.Code);
    }

    [Fact]
    public async Task A_pincode_without_a_known_state_is_refused_instead_of_guessing_the_tax()
    {
        var world = await WorldAsync();
        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync(pincode: "999999")).Id, "SellerToRetailer"));
        Assert.Equal("place_of_supply_unknown", error.Code);
    }

    [Fact]
    public async Task The_fee_statement_is_not_a_tax_invoice_and_uses_no_gst_number()
    {
        var world = await WorldAsync(sellerHasGstin: false, sellerHasAddress: false);
        var statement = await world.Service().CreateInvoiceAsync((await world.OrderAsync(SellerOrderStatus.Pending)).Id, "PlatformCommission");
        Assert.StartsWith("TC-", statement.InvoiceNumber);
        Assert.Empty(world.Db.InvoiceSequences);
    }

    private static async Task SetPlatformAsync(World world, string gstin, string name = "Fabricwala Pvt Ltd", string pincode = "302001")
    {
        foreach (var (key, value) in new[] { ("Platform.LegalName", name), ("Platform.Gstin", gstin), ("Platform.Address", "9 Tech Park"), ("Platform.Pincode", pincode), ("Platform.FeeGstRate", "18") })
            world.Db.PlatformSettings.Add(new TextileConnect.Domain.Catalogue.PlatformSetting { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow });
        await world.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_platform_company_is_complete_only_with_every_part_and_a_valid_gstin()
    {
        var world = await WorldAsync();
        Assert.Null(await PlatformCompany.LoadAsync(world.Db));
        await SetPlatformAsync(world, "08AAAAA0000A1Z9"); // wrong check character
        Assert.Null(await PlatformCompany.LoadAsync(world.Db));
        world.Db.PlatformSettings.Single(x => x.Key == "Platform.Gstin").Value = Gstin("08");
        await world.Db.SaveChangesAsync();
        var company = await PlatformCompany.LoadAsync(world.Db);
        Assert.NotNull(company);
        Assert.Equal(18m, company!.FeeGstRatePercent);
        Assert.Equal("08", company.StateCode);
    }

    [Fact]
    public async Task The_platform_fee_with_gst_is_a_tax_invoice_in_its_own_series()
    {
        var world = await WorldAsync();
        await SetPlatformAsync(world, Gstin("08"));
        var sellerInvoice = await world.Service().CreateInvoiceAsync((await world.OrderAsync(commissionGst: 3.60m)).Id, "SellerToRetailer");
        var order = await world.OrderAsync(commissionGst: 3.60m);
        var fee = await world.Service().CreateInvoiceAsync(order.Id, "PlatformCommission");
        var again = await world.Service().CreateInvoiceAsync(order.Id, "PlatformCommission");
        var second = await world.Service().CreateInvoiceAsync((await world.OrderAsync(commissionGst: 3.60m)).Id, "PlatformCommission");

        Assert.Equal("PF/2627/00001", fee.InvoiceNumber);
        Assert.Equal(fee.Id, again.Id);
        Assert.Equal("PF/2627/00002", second.InvoiceNumber);
        Assert.EndsWith("/2627/00001", sellerInvoice.InvoiceNumber);          // the seller series is untouched by the platform series
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(world.Storage.Files[fee.FileKey!], 0, 5));
    }

    [Fact]
    public async Task A_fee_invoice_needs_the_platform_company_and_a_dispatched_order()
    {
        var world = await WorldAsync();
        var noPlatform = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync(commissionGst: 3.60m)).Id, "PlatformCommission"));
        Assert.Equal("platform_details_missing", noPlatform.Code);
        await SetPlatformAsync(world, Gstin("08"));
        var early = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateInvoiceAsync((await world.OrderAsync(SellerOrderStatus.Accepted, commissionGst: 3.60m)).Id, "PlatformCommission"));
        Assert.Equal("invoice_not_ready", early.Code);
        Assert.Empty(world.Db.InvoiceSequences);
    }

    [Fact]
    public void The_fee_invoice_splits_the_tax_like_any_other_invoice()
    {
        GstParty Party(string name, string code) => new(name, "x", "a", "s", code);
        GstInvoice Fee(string platformCode) => GstInvoiceBuilder.Build(new GstInvoiceInput("PF/2627/00001", DateTime.UtcNow, Party("Platform", platformCode), Party("Seller", "08"), string.Empty, "Rajasthan", "08", "TC-1",
            [new GstItemInput("Platform fee", null, null, "998599", 1, 20m, 18m, 3.60m)], 0m, 23.60m));
        var same = Fee("08"); var other = Fee("07");
        Assert.False(same.InterState); Assert.Equal(1.80m, same.Cgst); Assert.Equal(1.80m, same.Sgst);
        Assert.True(other.InterState); Assert.Equal(3.60m, other.Igst);
    }

    // ---- credit notes ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1080, 50, 1080, 50, 1030)]       // refund of the whole order: all the GST comes back
    [InlineData(540, 50, 1080, 25, 515)]         // half the order: half the GST
    [InlineData(100, 50, 1080, 4.63, 95.37)]     // an odd share is rounded to the paisa and the two parts still add up
    [InlineData(100, 0, 1000, 0, 100)]           // an invoice without GST gives a credit note without GST
    [InlineData(100, 0, 0, 0, 100)]
    public void A_refund_takes_the_gst_share_of_the_invoice_out_of_the_credit_note(double refund, double tax, double total, double expectedTax, double expectedTaxable)
    {
        var (taxable, creditTax) = CreditNoteSplit.Split((decimal)refund, (decimal)tax, (decimal)total);
        Assert.Equal((decimal)expectedTax, creditTax);
        Assert.Equal((decimal)expectedTaxable, taxable);
        Assert.Equal((decimal)refund, taxable + creditTax);
    }

    private static async Task<TextileConnect.Domain.Payments.Refund> RefundAsync(World world, SellerOrder order, decimal amount, string status = RefundStatus.Processed)
    {
        var refund = new TextileConnect.Domain.Payments.Refund { PaymentId = 1, SellerOrderId = order.Id, Amount = amount, Reason = "Damaged pieces", Status = status };
        world.Db.Refunds.Add(refund);
        await world.Db.SaveChangesAsync();
        return refund;
    }

    [Fact]
    public async Task A_processed_refund_gets_a_credit_note_in_its_own_series_once()
    {
        var world = await WorldAsync();
        var order = await world.OrderAsync();
        var invoice = await world.Service().CreateInvoiceAsync(order.Id, "SellerToRetailer");
        var first = await RefundAsync(world, order, 540m);
        var second = await RefundAsync(world, order, 100m);

        var note = await world.Service().CreateCreditNoteAsync(first.Id);
        var again = await world.Service().CreateCreditNoteAsync(first.Id);
        var other = await world.Service().CreateCreditNoteAsync(second.Id);

        Assert.Equal($"C{world.Seller.Id}/2627/00001", note.InvoiceNumber);
        Assert.Equal(note.Id, again.Id);
        Assert.Equal($"C{world.Seller.Id}/2627/00002", other.InvoiceNumber);          // one note per refund, two refunds on one order are allowed
        Assert.Equal("CreditNote", note.InvoiceType);
        Assert.Equal(first.Id, note.RefundId);
        Assert.EndsWith("/2627/00001", invoice.InvoiceNumber);                          // the invoice series is not touched
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(world.Storage.Files[note.FileKey!], 0, 5));
    }

    [Fact]
    public async Task A_credit_note_is_refused_until_the_refund_has_gone_through()
    {
        var world = await WorldAsync();
        var order = await world.OrderAsync();
        await world.Service().CreateInvoiceAsync(order.Id, "SellerToRetailer");
        var refund = await RefundAsync(world, order, 100m, RefundStatus.Requested);

        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateCreditNoteAsync(refund.Id));
        Assert.Equal("refund_not_processed", error.Code);
        Assert.Equal(1, await world.Db.InvoiceSequences.CountAsync());              // only the series of the invoice exists, no number was used for the note
    }

    [Fact]
    public async Task A_refund_of_an_order_that_never_had_a_tax_invoice_has_nothing_to_correct()
    {
        var world = await WorldAsync();
        var order = await world.OrderAsync(SellerOrderStatus.Rejected);
        var refund = await RefundAsync(world, order, 1080m);

        var error = await Assert.ThrowsAsync<RuleViolationException>(async () => await world.Service().CreateCreditNoteAsync(refund.Id));
        Assert.Equal("credit_note_not_applicable", error.Code);
        await Assert.ThrowsAsync<KeyNotFoundException>(async () => await world.Service().CreateCreditNoteAsync(99999));
    }

    [Fact]
    public void The_invoice_numbers_and_types_are_pinned_in_the_model()
    {
        var invoice = new TextileConnect.Domain.Payments.Invoice { InvoiceNumber = "TC-20261113-00001", InvoiceType = "PlatformCommission", SellerOrderId = 4 };
        Assert.StartsWith("TC-", invoice.InvoiceNumber);
        Assert.Equal("PlatformCommission", invoice.InvoiceType);
    }
}
